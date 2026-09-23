using System;

namespace LetMeSleep.Audio
{
    /// <summary>Per-voice presentation targets. The engine glides to them; values are local, never authoritative.</summary>
    public struct VoicePlayoutParameters
    {
        /// <summary>Linear gain of the direct path (distance × occlusion).</summary>
        public float DirectGain;
        /// <summary>Linear gain into the room reverb.</summary>
        public float ReverbSend;
        /// <summary>Low-pass corner of the direct path (occlusion, distance), clamped to 500–5500 Hz.</summary>
        public float LowPassHz;
        /// <summary>0 = in front, 1 = fully behind the listener (gentle HF shelf cut and −1.5 dB).</summary>
        public float Behind;
        /// <summary>−1 below … +1 above the listener (small HF tilt).</summary>
        public float Elevation;
        public float ReverbDecaySeconds;
        public float ReverbDampingHz;
        /// <summary>Share of the mean channel gain blended into each channel (0 = hard pan, 0.5 = mono).</summary>
        public float Crossfeed;

        public static VoicePlayoutParameters Open(float gain = 1f) => new VoicePlayoutParameters
        {
            DirectGain = gain, ReverbSend = 0f, LowPassHz = VoicePlayoutEngine.OpenLowPassHz, Behind = 0f, Elevation = 0f,
            ReverbDecaySeconds = .4f, ReverbDampingHz = 3500f, Crossfeed = .15f
        };
    }

    /// <summary>
    /// Real-time voice playout for one remote speaker, consumed by the audio thread at DSP granularity
    /// (replaces the streamed AudioClip whose reader pulled 200–341 ms at a time and zero-filled the rest).
    ///
    /// Main thread: <see cref="Enqueue"/> 12 kHz PCM, <see cref="MarkStreamEnd"/>, <see cref="SetParameters"/>.
    /// Audio thread: <see cref="Render"/> multiplies the interleaved spatial gains Unity passes to
    /// OnAudioFilterRead (the AudioSource plays a constant 1.0 clip) by the processed voice.
    ///
    /// Stability: adaptive prebuffer (≥ 40 ms, grows 20 ms per underrun up to 160 ms; after 4 s without underruns
    /// it gives back a quarter of the excess, at least 5 ms, every 4 s),
    /// PI drift control (≤ ±0.6 % playback speed, inaudible) that holds the fill at the target instead of letting
    /// latency creep or dropping audio, underruns bridged by pitch-period concealment (up to
    /// 60 ms, then silence) with a 5 ms crossfade back to real audio, 5 ms fades on every start/stop, 10 ms
    /// end-of-stream fade, crossfaded catch-up skip when latency exceeds the target by 100 ms.
    /// Acoustics (12 kHz domain): smoothed direct gain, TPT low-pass, behind/elevation shelf, soft limiter,
    /// diffuse FDN room reverb; band-limited sinc upsampling to the output rate.
    /// </summary>
    public sealed class VoicePlayoutEngine
    {
        public const int InputRate = 12000;
        public const int RingCapacity = InputRate * 2;
        public const float OpenLowPassHz = 5500f;
        public const int FadeSamples = 60;          // 5 ms
        public const int EndFadeSamples = 120;      // 10 ms
        public const int CatchUpSamples = 1200;     // skip when the ring holds target + 100 ms
        public const int SkipCrossfadeSamples = 120;
        public const float MinimumTargetSeconds = .040f;
        public const float MaximumTargetSeconds = .160f;
        private const int TargetStepUp = 240;       // +20 ms per starvation
        private const int TargetStepDown = 60;      // at least −5 ms per calm period
        private const double CalmSecondsBeforeShrink = 4.0;
        private const float MaximumDrift = .006f;
        private const float DriftProportional = .3f;   // speed per second of fill error
        private const float DriftIntegral = .05f;      // speed per second² of fill error (removes steady-state error)
        private const float DriftIntegralLimit = .005f;
        private const int ParameterBlock = 16;
        private const int HalfWidth = 6;
        private const int History = HalfWidth * 2;
        private const int Phases = 128;
        private static readonly float[] PhaseWeights = BuildPhaseWeights();   // (Phases + 1) × History, rows sum to 1

        // ---- shared state (guarded by gate) ----
        private readonly object gate = new object();
        private readonly float[] ring = new float[RingCapacity];
        private readonly long[] endMarkers = new long[8];
        private int readIndex, writeIndex, available, markerCount, generation;
        private long writtenTotal, readTotal;
        private bool fadeClearRequested, resetRequested, pendingDirty, pendingSnap;
        private VoicePlayoutParameters pending = VoicePlayoutParameters.Open();
        private volatile int outputRate = 48000;

        // ---- audio-thread state ----
        private enum Phase { Buffering, Playing, Concealing, Recovering }
        private Phase phase = Phase.Buffering;
        private float fade, fadeTarget;
        private int targetSamples = -1, minimumTarget, lastFrames, renderedRate;
        private double renderedSeconds, lastStarveAt, lastShrinkAt;
        private float fillAverage, speed = 1f, driftIntegral;
        private double resamplePhase;
        private readonly float[] dryHistory = new float[History];
        private readonly float[] wetLeftHistory = new float[History];
        private readonly float[] wetRightHistory = new float[History];
        private int historyHead;
        private int skipRemaining, skipDistance, recoverPosition;
        private readonly VoiceConcealer underrun = new VoiceConcealer();
        private int parameterCounter;
        private VoicePlayoutParameters active = VoicePlayoutParameters.Open();
        private VoiceSmoother directGain, reverbSend, lowPassLog, shelfDb, levelDb, crossfeed;
        private readonly VoiceSvf lowPass = new VoiceSvf();
        private readonly VoiceSvf shelf = new VoiceSvf();
        private readonly VoiceRoomReverb reverb = new VoiceRoomReverb();
        private float shelfGain = 1f, overallGain = 1f;
        private int localRead, localAvailable, consumed;
        private long localReadTotal;
        private int localMarkerCount;
        private readonly long[] localMarkers = new long[8];
        private bool clearAfterFade;

        // ---- statistics (written by the audio thread) ----
        private volatile int starvations, rebuffers, skips, renders;
        private volatile float outputLevel, silentSeconds, currentTargetSeconds, lastFillSeconds;
        private long renderedInputSamples, droppedSamples;

        public VoicePlayoutEngine()
        {
            directGain.Configure(.012f, InputRate); directGain.Reset(1f);
            reverbSend.Configure(.03f, InputRate); reverbSend.Reset(0f);
            lowPassLog.Configure(.04f, InputRate / (float)ParameterBlock); lowPassLog.Reset((float)Math.Log(OpenLowPassHz));
            shelfDb.Configure(.05f, InputRate / (float)ParameterBlock); shelfDb.Reset(0f);
            levelDb.Configure(.05f, InputRate / (float)ParameterBlock); levelDb.Reset(0f);
            crossfeed.Configure(.05f, InputRate / (float)ParameterBlock); crossfeed.Reset(.15f);
            lowPass.SetCoefficients(OpenLowPassHz, InputRate, .7071f);
            shelf.SetCoefficients(2800f, InputRate, .7071f);
        }

        public int Starvations => starvations;
        public int Rebuffers => rebuffers;
        public int Skips => skips;
        public int RenderCalls => renders;
        public float OutputLevel => outputLevel;
        public float SilentSeconds => silentSeconds;
        public float TargetSeconds => currentTargetSeconds;
        public float LastFillSeconds => lastFillSeconds;
        public long RenderedInputSamples { get { lock (gate) return renderedInputSamples; } }
        public long DroppedSamples { get { lock (gate) return droppedSamples; } }
        public int BufferedSamples { get { lock (gate) return available; } }
        public bool HasPendingAudio { get { lock (gate) return available > 0; } }

        public int OutputRate
        {
            get => outputRate;
            set { if (value >= 8000 && value <= 384000) outputRate = value; }
        }

        /// <summary>Appends processed 12 kHz samples. Returns how many were accepted (never overwrites unread audio).</summary>
        public int Enqueue(float[] samples, int offset, int count)
        {
            if (samples == null || offset < 0 || count < 0 || offset + count > samples.Length) throw new ArgumentOutOfRangeException(nameof(samples));
            lock (gate)
            {
                int space = RingCapacity - available;
                int accepted = Math.Min(space, count);
                droppedSamples += count - accepted;
                for (int i = 0; i < accepted; i++)
                {
                    ring[writeIndex] = VoiceDsp.Finite(samples[offset + i]);
                    if (++writeIndex == RingCapacity) writeIndex = 0;
                }
                available += accepted;
                writtenTotal += accepted;
                return accepted;
            }
        }

        /// <summary>The current stream ends after the audio already queued: it drains with a 10 ms fade.</summary>
        public void MarkStreamEnd()
        {
            lock (gate)
            {
                if (markerCount > 0 && endMarkers[markerCount - 1] == writtenTotal) return;
                if (markerCount == endMarkers.Length) { Array.Copy(endMarkers, 1, endMarkers, 0, endMarkers.Length - 1); markerCount--; }
                endMarkers[markerCount++] = writtenTotal;
            }
        }

        public void SetParameters(in VoicePlayoutParameters parameters, bool snap = false)
        {
            lock (gate) { pending = parameters; pendingDirty = true; pendingSnap |= snap; }
        }

        /// <summary>Fades the voice out on the audio thread (5 ms) and then drops everything queued.</summary>
        public void RequestFadeClear() { lock (gate) fadeClearRequested = true; }

        /// <summary>Drops queued audio immediately. Use only when the audio thread is not rendering this voice.</summary>
        public void ClearNow()
        {
            lock (gate)
            {
                readIndex = writeIndex; readTotal = writtenTotal; available = 0; markerCount = 0;
                generation++; fadeClearRequested = false; resetRequested = true;
            }
        }

        /// <summary>
        /// Audio thread. <paramref name="data"/> holds the interleaved spatial gains of the AudioSource (a constant
        /// 1.0 clip after Unity's pan and attenuation); it is overwritten with the voice.
        /// </summary>
        public void Render(float[] data, int channels)
        {
            if (data == null || channels <= 0) return;
            int frames = data.Length / channels;
            if (frames <= 0) return;
            renders++;

            int gen;
            bool clearRequested;
            lock (gate)
            {
                if (resetRequested) { ResetAudioThreadState(); resetRequested = false; }
                if (pendingDirty) { ApplyParameters(pending, pendingSnap); pendingDirty = false; pendingSnap = false; }
                gen = generation;
                localRead = readIndex; localAvailable = available; localReadTotal = readTotal; consumed = 0;
                localMarkerCount = markerCount;
                Array.Copy(endMarkers, localMarkers, markerCount);
                clearRequested = fadeClearRequested;
            }

            int rate = outputRate;
            if (rate != renderedRate || frames != lastFrames) ConfigureForOutput(rate, frames);
            if (clearRequested) { clearAfterFade = true; fadeTarget = 0f; }
            UpdateBlockControl(frames, rate);

            double step = InputRate / (double)rate * speed;
            double sumSquares = 0;
            bool anyOutput = false;
            for (int i = 0; i < frames; i++)
            {
                resamplePhase += step;
                while (resamplePhase >= 1.0)
                {
                    resamplePhase -= 1.0;
                    NextInputSample(out float dry, out float wetL, out float wetR);
                    dryHistory[historyHead] = dry; wetLeftHistory[historyHead] = wetL; wetRightHistory[historyHead] = wetR;
                    if (++historyHead == History) historyHead = 0;
                }

                float frac = (float)resamplePhase;
                float dryOut = 0f;
                // Newest sample sits at historyHead-1; interpolate between (newest - HalfWidth) and the next one
                // with polyphase weights (linear between the two nearest of 128 precomputed phases).
                float position = frac * Phases;
                int phaseRow = (int)position;
                if (phaseRow >= Phases) phaseRow = Phases - 1;
                float blend = position - phaseRow;
                int rowA = phaseRow * History, rowB = rowA + History;
                int start = historyHead; // oldest sample
                for (int k = 0; k < History; k++)
                {
                    int index = start + k; if (index >= History) index -= History;
                    float w = PhaseWeights[rowA + k] + (PhaseWeights[rowB + k] - PhaseWeights[rowA + k]) * blend;
                    dryOut += dryHistory[index] * w;
                }
                int left = start + HalfWidth - 1; if (left >= History) left -= History;
                float wetOutL = Cubic(wetLeftHistory, left, frac);
                float wetOutR = Cubic(wetRightHistory, left, frac);

                int baseIndex = i * channels;
                if (channels == 1)
                {
                    float g = data[baseIndex];
                    data[baseIndex] = dryOut * g + .5f * (wetOutL + wetOutR) * Math.Abs(g);
                }
                else
                {
                    float mean = 0f, power = 0f;
                    for (int c = 0; c < channels; c++) { float g = data[baseIndex + c]; mean += g; power += g * g; }
                    mean /= channels;
                    float wetScale = (float)Math.Sqrt(power) * .7071f;
                    float x = crossfeed.Value;
                    for (int c = 0; c < channels; c++)
                    {
                        float g = data[baseIndex + c];
                        float mixed = g + (mean - g) * x;
                        data[baseIndex + c] = dryOut * mixed + ((c & 1) == 0 ? wetOutL : wetOutR) * wetScale;
                    }
                }
                sumSquares += dryOut * dryOut;
                if (dryOut != 0f || wetOutL != 0f) anyOutput = true;
            }

            renderedSeconds += frames / (double)rate;
            float rms = (float)Math.Sqrt(sumSquares / frames);
            outputLevel = rms;
            silentSeconds = anyOutput || localAvailable - consumed > 0 ? 0f : silentSeconds + frames / (float)rate;

            lock (gate)
            {
                renderedInputSamples += consumed;
                if (gen == generation)
                {
                    readIndex = localRead; available -= consumed; readTotal = localReadTotal;
                    // Drop markers the audio thread already passed.
                    int passed = 0;
                    while (passed < markerCount && endMarkers[passed] <= readTotal) passed++;
                    if (passed > 0)
                    {
                        Array.Copy(endMarkers, passed, endMarkers, 0, markerCount - passed);
                        markerCount -= passed;
                    }
                    if (clearAfterFade && fade <= 0f)
                    {
                        readIndex = writeIndex; readTotal = writtenTotal; available = 0; markerCount = 0;
                        fadeClearRequested = false; clearAfterFade = false; phase = Phase.Buffering;
                    }
                }
                lastFillSeconds = available / (float)InputRate;
            }
        }

        private void ConfigureForOutput(int rate, int frames)
        {
            renderedRate = rate; lastFrames = frames;
            // One DSP block of input plus a main-thread frame of slack, never below 40 ms.
            int block = (int)Math.Ceiling(frames * (double)InputRate / rate);
            minimumTarget = Math.Max((int)(MinimumTargetSeconds * InputRate), (int)(block * 1.5) + 120);
            int maximum = (int)(MaximumTargetSeconds * InputRate);
            if (minimumTarget > maximum) minimumTarget = maximum;
            if (targetSamples < minimumTarget) targetSamples = minimumTarget + FadeSamples;
            currentTargetSeconds = targetSamples / (float)InputRate;
        }

        private void UpdateBlockControl(int frames, int rate)
        {
            int buffered = localAvailable;
            if (phase == Phase.Playing)
            {
                float blockSeconds = frames / (float)rate;
                float alpha = 1f - (float)Math.Exp(-blockSeconds / .6f);
                fillAverage += (buffered - fillAverage) * alpha;
                // PI control of the fill level: positive error = too much latency = play slightly faster.
                float error = (fillAverage - targetSamples) / InputRate;
                if (Math.Abs(error) < .06f)
                    driftIntegral = VoiceDsp.Clamp(driftIntegral + error * blockSeconds * DriftIntegral, -DriftIntegralLimit, DriftIntegralLimit);
                speed = 1f + VoiceDsp.Clamp(error * DriftProportional + driftIntegral, -MaximumDrift, MaximumDrift);
                if (skipRemaining == 0 && buffered > targetSamples + CatchUpSamples)
                {
                    skipDistance = buffered - targetSamples - 240;
                    skipRemaining = SkipCrossfadeSamples;
                    skips++;
                }
            }
            else { speed = 1f; fillAverage = buffered; }

            if (renderedSeconds - lastStarveAt > CalmSecondsBeforeShrink && renderedSeconds - lastShrinkAt > CalmSecondsBeforeShrink &&
                targetSamples > minimumTarget)
            {
                int step = Math.Max(TargetStepDown, (targetSamples - minimumTarget) / 4);
                targetSamples = Math.Max(minimumTarget, targetSamples - step);
                lastShrinkAt = renderedSeconds;
                currentTargetSeconds = targetSamples / (float)InputRate;
            }
        }

        private void NextInputSample(out float dry, out float wetLeft, out float wetRight)
        {
            float raw = PullRaw();

            if (++parameterCounter >= ParameterBlock)
            {
                parameterCounter = 0;
                lowPassLog.Next(); shelfDb.Next(); levelDb.Next(); crossfeed.Next();
                lowPass.SetCoefficients((float)Math.Exp(lowPassLog.Value), InputRate, .7071f);
                shelfGain = VoiceDsp.DbToGain(shelfDb.Value);
                overallGain = VoiceDsp.DbToGain(levelDb.Value);
            }
            float g = directGain.Next();
            float send = reverbSend.Next();

            lowPass.Process(raw);
            float filtered = lowPass.Low;
            shelf.Process(filtered);
            float shaped = filtered + (shelfGain - 1f) * shelf.High;
            dry = VoiceDsp.SoftLimit(shaped * g * overallGain);

            float reverbInput = filtered * send;
            if (reverbInput != 0f || reverb.IsRinging) reverb.Process(reverbInput, out wetLeft, out wetRight);
            else { wetLeft = 0f; wetRight = 0f; }
        }

        /// <summary>Takes the next 12 kHz sample from the ring according to the buffering state machine.</summary>
        private float PullRaw()
        {
            int remaining = localAvailable - consumed;
            long nextEnd = localMarkerCount > 0 ? localMarkers[0] : long.MaxValue;
            long toEnd = nextEnd - localReadTotal;

            if (toEnd <= 0 && localMarkerCount > 0)
            {
                // Passed an end marker: the next samples (if any) belong to a new stream.
                Array.Copy(localMarkers, 1, localMarkers, 0, localMarkerCount - 1);
                localMarkerCount--;
                phase = Phase.Buffering; fade = 0f; fadeTarget = 0f;
                underrun.Reset();
                nextEnd = localMarkerCount > 0 ? localMarkers[0] : long.MaxValue;
                toEnd = nextEnd - localReadTotal;
            }

            switch (phase)
            {
                case Phase.Buffering:
                    if (clearAfterFade || remaining <= 0) return 0f;
                    if (remaining >= targetSamples || toEnd <= remaining)
                    {
                        phase = Phase.Playing; fadeTarget = 1f;
                        if (toEnd <= remaining && remaining < targetSamples) fade = 1f; // short utterance: sender already faded
                    }
                    else return 0f;
                    break;
                case Phase.Playing:
                    if (remaining <= 0 && !clearAfterFade)
                    {
                        // Underrun (late network or a main-thread hitch): continue the voice instead of a hole.
                        phase = Phase.Concealing; starvations++;
                        underrun.BeginLoss();
                        lastStarveAt = renderedSeconds;
                        targetSamples = Math.Min((int)(MaximumTargetSeconds * InputRate), targetSamples + TargetStepUp);
                        currentTargetSeconds = targetSamples / (float)InputRate;
                    }
                    break;
            }

            if (phase == Phase.Concealing)
            {
                if (!clearAfterFade && remaining > 0 && (toEnd <= remaining || remaining >= Math.Max(FadeSamples * 2, targetSamples / 2)))
                {
                    phase = Phase.Recovering; recoverPosition = 0;
                }
                else
                {
                    float synthetic = underrun.NextSynthetic();
                    if (underrun.Exhausted)
                    {
                        // Long gap: the continuation already faded out; restart with a normal fade-in.
                        underrun.EndLoss(); phase = Phase.Buffering; fade = 0f; fadeTarget = 0f; rebuffers++;
                    }
                    return synthetic * VoiceDsp.Ramp(fade);
                }
            }

            if (remaining <= 0) { if (phase != Phase.Buffering) { phase = Phase.Buffering; rebuffers++; } fade = 0f; return 0f; }

            float sample = ring[localRead];
            if (skipRemaining > 0 && skipDistance > 0 && skipDistance < remaining - SkipCrossfadeSamples)
            {
                int far = localRead + skipDistance; if (far >= RingCapacity) far -= RingCapacity;
                float w = VoiceDsp.Ramp(1f - (skipRemaining - 1) / (float)SkipCrossfadeSamples);
                sample = sample * (1f - w) + ring[far] * w;
                if (--skipRemaining == 0)
                {
                    Advance(skipDistance);
                    skipDistance = 0;
                }
            }
            else if (skipRemaining > 0) { skipRemaining = 0; skipDistance = 0; }
            Advance(1);
            underrun.Observe(sample);

            if (phase == Phase.Recovering)
            {
                float w = VoiceDsp.Ramp((recoverPosition + 1) / (float)(VoiceConcealer.RecoveryCrossfade + 1));
                sample = underrun.NextSynthetic() * (1f - w) + sample * w;
                if (++recoverPosition >= VoiceConcealer.RecoveryCrossfade) { phase = Phase.Playing; underrun.EndLoss(); }
            }

            if (fade < fadeTarget) fade = Math.Min(fadeTarget, fade + 1f / FadeSamples);
            else if (fade > fadeTarget) fade = Math.Max(fadeTarget, fade - 1f / FadeSamples);
            float gain = VoiceDsp.Ramp(fade);
            if (toEnd - 1 < EndFadeSamples) gain *= VoiceDsp.Ramp((toEnd - 1) / (float)EndFadeSamples);
            return sample * gain;
        }

        private void Advance(int count)
        {
            localRead += count; if (localRead >= RingCapacity) localRead -= RingCapacity;
            consumed += count; localReadTotal += count;
        }

        private void ApplyParameters(in VoicePlayoutParameters p, bool snap)
        {
            active = p;
            directGain.Target = VoiceDsp.Clamp(VoiceDsp.Finite(p.DirectGain), 0f, 4f);
            reverbSend.Target = VoiceDsp.Clamp(VoiceDsp.Finite(p.ReverbSend), 0f, 2f);
            lowPassLog.Target = (float)Math.Log(VoiceDsp.Clamp(p.LowPassHz > 0 ? p.LowPassHz : OpenLowPassHz, 500f, OpenLowPassHz));
            float behind = VoiceDsp.Clamp01(VoiceDsp.Finite(p.Behind));
            float elevation = VoiceDsp.Clamp(VoiceDsp.Finite(p.Elevation), -1f, 1f);
            shelfDb.Target = -5f * behind + 2f * Math.Max(0f, elevation) - 2f * Math.Max(0f, -elevation);
            levelDb.Target = -1.5f * behind;
            crossfeed.Target = VoiceDsp.Clamp(VoiceDsp.Finite(p.Crossfeed), 0f, .5f);
            reverb.SetTargets(p.ReverbDecaySeconds > 0 ? p.ReverbDecaySeconds : .4f, p.ReverbDampingHz > 0 ? p.ReverbDampingHz : 3500f);
            if (snap)
            {
                directGain.Reset(directGain.Target); reverbSend.Reset(reverbSend.Target);
                lowPassLog.Reset(lowPassLog.Target); shelfDb.Reset(shelfDb.Target); levelDb.Reset(levelDb.Target);
                crossfeed.Reset(crossfeed.Target); reverb.SnapToTargets();
                lowPass.SetCoefficients((float)Math.Exp(lowPassLog.Value), InputRate, .7071f);
                shelfGain = VoiceDsp.DbToGain(shelfDb.Value); overallGain = VoiceDsp.DbToGain(levelDb.Value);
            }
        }

        private void ResetAudioThreadState()
        {
            phase = Phase.Buffering; fade = fadeTarget = 0f; resamplePhase = 0; speed = 1f; fillAverage = 0f;
            Array.Clear(dryHistory, 0, History); Array.Clear(wetLeftHistory, 0, History); Array.Clear(wetRightHistory, 0, History);
            historyHead = 0; skipRemaining = skipDistance = 0; clearAfterFade = false; recoverPosition = 0;
            lowPass.Reset(); shelf.Reset(); reverb.Reset(); underrun.Reset();
        }

        private static float[] BuildPhaseWeights()
        {
            var kernel = new VoiceSincKernel(HalfWidth, .9f, 6.5f);
            var table = new float[(Phases + 1) * History];
            for (int p = 0; p <= Phases; p++)
            {
                float frac = p / (float)Phases;
                double sum = 0;
                for (int k = 0; k < History; k++)
                {
                    float w = kernel.Evaluate(k - HalfWidth + 1 - frac); // taps −5 … +6 around the left neighbour
                    table[p * History + k] = w; sum += w;
                }
                for (int k = 0; k < History; k++) table[p * History + k] = (float)(table[p * History + k] / sum);
            }
            return table;
        }

        private static float Cubic(float[] history, int left, float t)
        {
            int n = History;
            int im1 = left - 1; if (im1 < 0) im1 += n;
            int i1 = left + 1; if (i1 >= n) i1 -= n;
            int i2 = left + 2; if (i2 >= n) i2 -= n;
            float y0 = history[im1], y1 = history[left], y2 = history[i1], y3 = history[i2];
            float c0 = y1, c1 = .5f * (y2 - y0), c2 = y0 - 2.5f * y1 + 2f * y2 - .5f * y3, c3 = .5f * (y3 - y0) + 1.5f * (y1 - y2);
            return ((c3 * t + c2) * t + c1) * t + c0;
        }
    }
}

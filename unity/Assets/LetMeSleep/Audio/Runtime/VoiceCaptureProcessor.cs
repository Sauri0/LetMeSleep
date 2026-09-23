using System;

namespace LetMeSleep.Audio
{
    /// <summary>
    /// Sender-side conditioning of 20 ms microphone frames at 12 kHz, applied before encoding:
    /// 90 Hz high-pass (DC, rumble, handling noise) → noise gate with hysteresis, 200 ms hold and a −24 dB floor
    /// (noise floor = minimum frame level over the last 2 s; the open threshold stays ≥ 15 dB under the learned
    /// speech level and is raised while remote voices play, a simple half-duplex echo guard) → slow AGC toward −18 dBFS RMS of
    /// speech (−8…+20 dB, learns only while the gate is open) → soft limiter (≤ −1 dBFS) → push-to-talk
    /// envelope (20 ms raised-cosine fade-in; the release tail of 40 ms fades to zero before the stream ends).
    /// Pure C#: deterministic and testable without a microphone.
    /// </summary>
    public sealed class VoiceCaptureProcessor
    {
        public const int SampleRate = 12000;
        public const int FrameSamples = 240;
        public const float TargetSpeechDbfs = -18f;
        public const float MinimumAgcDb = -8f;
        public const float MaximumAgcDb = 20f;
        public const float GateFloorDb = -24f;
        public const float FadeInSeconds = .020f;
        public const float ReleaseTailSeconds = .040f;
        public const int FadeInSamples = (int)(FadeInSeconds * SampleRate);
        public const int ReleaseTailSamples = (int)(ReleaseTailSeconds * SampleRate);
        private const float GateOpenAboveNoiseDb = 9f;
        private const float GateHysteresisDb = 4f;
        private const float GateAbsoluteOpenDbfs = -58f;
        private const float GateHoldSeconds = .2f;
        private const float EchoMarginDb = 12f;
        private const float SpeechHeadroomDb = 15f;
        private const int NoiseWindowFrames = 100;      // 2 s of 20 ms frames

        private readonly VoiceSvf highPass = new VoiceSvf();
        private VoiceSmoother gateGain, agcGain;
        private float noiseFloorDb = -65f, speechLevelDb = TargetSpeechDbfs, agcDb, farEndDb = -120f;
        private float holdRemaining;
        private readonly float[] levelWindow = new float[NoiseWindowFrames];
        private int levelWrite, levelCount;
        private bool gateOpen, releasing, released;
        private int fadeInPosition, releasePosition;

        public VoiceCaptureProcessor()
        {
            highPass.SetCoefficients(90f, SampleRate, .7071f);
            gateGain.Configure(.004f, SampleRate);
            agcGain.Configure(.05f, SampleRate);
            Reset(true);
        }

        public bool GateOpen => gateOpen;
        public float NoiseFloorDbfs => noiseFloorDb;
        public float AgcGainDb => agcDb;
        public float LastInputDbfs { get; private set; } = -120f;
        public float LastOutputDbfs { get; private set; } = -120f;
        /// <summary>True once the release tail has faded to silence: the capture can stop and send End.</summary>
        public bool Released => released;
        public bool Releasing => releasing;

        /// <summary>Starts a new push-to-talk transmission (fade-in). AGC and noise estimates carry over.</summary>
        public void BeginTransmission()
        {
            highPass.Reset();
            fadeInPosition = 0; releasePosition = 0; releasing = false; released = false;
            gateGain.Reset(gateOpen ? 1f : VoiceDsp.DbToGain(GateFloorDb));
            holdRemaining = 0f;
        }

        /// <summary>Key released: the next <see cref="ReleaseTailSamples"/> samples fade out, then <see cref="Released"/>.</summary>
        public void BeginRelease()
        {
            if (!releasing) { releasing = true; releasePosition = 0; }
        }

        /// <summary>Forgets learned levels (new microphone).</summary>
        public void Reset(bool forgetLevels)
        {
            highPass.Reset();
            if (forgetLevels)
            {
                noiseFloorDb = -65f; speechLevelDb = TargetSpeechDbfs; agcDb = 0f; agcGain.Reset(1f);
                levelWrite = 0; levelCount = 0;
            }
            gateOpen = false; gateGain.Reset(VoiceDsp.DbToGain(GateFloorDb));
            fadeInPosition = 0; releasePosition = 0; releasing = false; released = false; holdRemaining = 0f;
        }

        /// <summary>RMS (linear) of the remote voices currently playing locally; raises the gate against echo.</summary>
        public void SetFarEndLevel(float rms)
        {
            farEndDb = VoiceDsp.GainToDb(VoiceDsp.Clamp(VoiceDsp.Finite(rms), 0f, 1f));
        }

        /// <summary>Processes one frame and returns a new 240-sample array.</summary>
        public float[] Process(float[] frame)
        {
            if (frame == null || frame.Length != FrameSamples) throw new ArgumentException("Capture frames must contain 240 samples.", nameof(frame));
            var output = new float[FrameSamples];
            double sum = 0;
            for (int i = 0; i < FrameSamples; i++)
            {
                highPass.Process(VoiceDsp.Clamp(VoiceDsp.Finite(frame[i]), -1f, 1f));
                output[i] = highPass.High;
                sum += output[i] * output[i];
            }
            float levelDb = VoiceDsp.GainToDb((float)Math.Sqrt(sum / FrameSamples));
            LastInputDbfs = levelDb;
            float frameSeconds = FrameSamples / (float)SampleRate;

            // Noise floor: minimum statistics over the last 2 s (speech pauses and syllable troughs).
            levelWindow[levelWrite] = levelDb;
            levelWrite = (levelWrite + 1) % NoiseWindowFrames;
            if (levelCount < NoiseWindowFrames) levelCount++;
            float minimum = float.MaxValue;
            for (int i = 0; i < levelCount; i++) if (levelWindow[i] < minimum) minimum = levelWindow[i];
            noiseFloorDb = VoiceDsp.Clamp(minimum, -90f, -30f);

            float openThreshold = Math.Max(noiseFloorDb + GateOpenAboveNoiseDb, GateAbsoluteOpenDbfs);
            // Continuous speech must never gate itself: stay well under the learned speech level.
            openThreshold = Math.Min(openThreshold, speechLevelDb - SpeechHeadroomDb);
            if (farEndDb > -80f) openThreshold = Math.Max(openThreshold, farEndDb - EchoMarginDb);
            float closeThreshold = openThreshold - GateHysteresisDb;
            if (levelDb >= openThreshold) { gateOpen = true; holdRemaining = GateHoldSeconds; }
            else if (levelDb < closeThreshold)
            {
                holdRemaining -= frameSeconds;
                if (holdRemaining <= 0f) gateOpen = false;
            }

            // AGC learns from confident speech only; gain moves ≤ 10 dB/s up and ≤ 30 dB/s down.
            if (gateOpen && levelDb > noiseFloorDb + 6f && levelDb > -65f)
            {
                speechLevelDb += (levelDb - speechLevelDb) * (1f - (float)Math.Exp(-frameSeconds / .4f));
                float desired = VoiceDsp.Clamp(TargetSpeechDbfs - speechLevelDb, MinimumAgcDb, MaximumAgcDb);
                float maxStep = (desired > agcDb ? 10f : 30f) * frameSeconds;
                agcDb += VoiceDsp.Clamp(desired - agcDb, -maxStep, maxStep);
            }
            agcGain.Target = VoiceDsp.DbToGain(agcDb);
            gateGain.Target = gateOpen ? 1f : VoiceDsp.DbToGain(GateFloorDb);
            // Open fast (4 ms), close slowly (≈ 120 ms): no chopped word onsets, no pumping between words.
            gateGain.Configure(gateOpen ? .004f : .12f, SampleRate);

            double outSum = 0;
            for (int i = 0; i < FrameSamples; i++)
            {
                float envelope = 1f;
                if (fadeInPosition < FadeInSamples) envelope = VoiceDsp.Ramp(++fadeInPosition / (float)FadeInSamples);
                if (releasing)
                {
                    envelope *= VoiceDsp.Ramp(1f - releasePosition / (float)ReleaseTailSamples);
                    if (releasePosition < ReleaseTailSamples) releasePosition++;
                    else released = true;
                }
                float value = output[i] * gateGain.Next() * agcGain.Next();
                value = VoiceDsp.SoftLimit(value, .6f, .891f) * envelope;
                output[i] = value;
                outSum += value * value;
            }
            if (releasing && releasePosition >= ReleaseTailSamples) released = true;
            LastOutputDbfs = VoiceDsp.GainToDb((float)Math.Sqrt(outSum / FrameSamples));
            return output;
        }
    }
}

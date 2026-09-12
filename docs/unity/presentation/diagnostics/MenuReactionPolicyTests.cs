using System;
using LetMeSleep.Presentation;
using Beat = LetMeSleep.Presentation.MenuReactionPolicy.Beat;
internal static class MenuReactionPolicyTests
{
    static int checks;
    static void Check(string name, Beat state, float seconds, float distance, float predicted, Beat expected, bool reduced=false)
    {
        var actual=MenuReactionPolicy.Next(state,seconds,1.2f,2f,1.2f,1.6f,14f,distance,predicted,1.8f,.7f,reduced);
        if(actual!=expected) throw new Exception(name+": expected "+expected+", got "+actual);
        checks++;
    }
    static void Main()
    {
        Check("far mosquito never triggers attention",Beat.Idle,30,3,2.5f,Beat.Idle);
        Check("entry respects short cooldown",Beat.Idle,1,1,.6f,Beat.Idle);
        Check("near mosquito triggers attention",Beat.Idle,1.2f,1,.6f,Beat.Look);
        Check("prepare before swatting",Beat.Look,1,1,.6f,Beat.Look);
        Check("near approach triggers swat",Beat.Look,2,1,.6f,Beat.Swat);
        Check("near receding mosquito does not trigger swat",Beat.Look,2,.5f,.6f,Beat.Look);
        Check("future miss does not trigger swat",Beat.Look,2,1,.8f,Beat.Look);
        Check("distant approach does not trigger swat",Beat.Look,2,2,.6f,Beat.Look);
        Check("departed mosquito settles gaze",Beat.Look,3,3,3.2f,Beat.Settle);
        Check("missed approach eventually settles",Beat.Look,15,1,1,Beat.Settle);
        Check("swat completes into return",Beat.Swat,1.2f,.4f,.8f,Beat.Return);
        Check("return completes into idle",Beat.Return,1.6f,3,3,Beat.Idle);
        Check("settle completes into idle",Beat.Settle,.4f,3,3,Beat.Idle);
        foreach(Beat state in Enum.GetValues(typeof(Beat))) Check("reduced cancels "+state,state,3,.4f,.2f,Beat.Idle,true);
        Check("resume begins in idle with cooldown",Beat.Idle,0,.4f,.2f,Beat.Idle);
        var weights=new float[4];
        BlinkWeightPolicy.Fill(0,weights);
        if(Array.Exists(weights,x=>x!=0)) throw new Exception("open must preserve Basis"); checks++;
        BlinkWeightPolicy.Fill(.4f,weights);
        if(Math.Abs(weights[0]-40)> .001f || Math.Abs(weights[1]-60)>.001f || weights[2]!=0 || weights[3]!=0) throw new Exception("40 percent closure must blend only 25/50 samples"); checks++;
        BlinkWeightPolicy.Fill(1,weights);
        if(weights[3]!=100 || weights[0]!=0 || weights[1]!=0 || weights[2]!=0) throw new Exception("closed endpoint"); checks++;
        for(int i=0;i<=100;i++)
        {
            BlinkWeightPolicy.Fill(i/100f,weights);
            int active=0; float total=0;
            foreach(float weight in weights) { if(weight<0 || weight>100) throw new Exception("invalid morph weight"); if(weight>0) active++; total+=weight; }
            if(active>2 || total>100.001f) throw new Exception("morph samples overlap or exceed full weight");
        }
        checks++;
        Console.WriteLine(checks+" policy checks passed. Scope: pure state decisions; no Unity animation/render/lifecycle execution.");
    }
}

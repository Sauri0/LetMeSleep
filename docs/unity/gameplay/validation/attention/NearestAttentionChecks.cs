using System;
using LetMeSleep.Presentation.Gameplay;
using Policy = LetMeSleep.Presentation.Gameplay.NearestAttentionPolicy<AttentionActor>;

public sealed class AttentionActor
{
    public string Role;
    public AttentionActor(string role = "Human") { Role = role; }
}
public static class NearestAttentionChecks
{
    private static int cases, failures;
    private static Policy.Candidate C(AttentionActor actor, float d, float dot = 1) => new Policy.Candidate(actor, d, dot);
    private static void Same(object expected, object actual) { if (!ReferenceEquals(expected, actual)) throw new Exception("Unexpected selected instance"); }
    private static void Check(bool ok) { if (!ok) throw new Exception("Condition failed"); }
    private static void Run(string name, Action body)
    { cases++; try { body(); Console.WriteLine("PASS " + name); } catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
    public static int Main()
    {
        Run("B1.9_no_B2_yes", () => {
            var a=new AttentionActor();var b=new AttentionActor();var p=new Policy();
            p.Select(new[]{C(a,2)},0);p.Select(new[]{C(a,2),C(b,1)},0);
            Same(a,p.Select(new[]{C(a,2),C(b,1)},1.9));Same(b,p.Select(new[]{C(a,2),C(b,1)},2));
        });
        Run("alternating_B_C_does_not_accumulate", () => {
            var a=new AttentionActor();var b=new AttentionActor();var c=new AttentionActor();var p=new Policy();
            p.Select(new[]{C(a,3)},0);
            for(int i=0;i<5;i++) Same(a,p.Select(new[]{C(a,3),C(i%2==0?b:c,1)},i));
            Same(a,p.Select(new[]{C(a,3),C(b,1)},5.9));Same(b,p.Select(new[]{C(a,3),C(b,1)},6));
        });
        Run("current_tie_wins_and_resets_wait", () => {
            var a=new AttentionActor();var b=new AttentionActor();var p=new Policy();
            p.Select(new[]{C(a,2)},0);p.Select(new[]{C(a,2),C(b,1)},0);
            Same(a,p.Select(new[]{C(b,2),C(a,2)},1.9));
            Same(a,p.Select(new[]{C(a,2),C(b,1)},2));Same(a,p.Select(new[]{C(a,2),C(b,1)},3.9));
            Same(b,p.Select(new[]{C(a,2),C(b,1)},4));
        });
        Run("candidate_not_closer_resets_wait", () => {
            var a=new AttentionActor();var b=new AttentionActor();var p=new Policy();
            p.Select(new[]{C(a,2)},0);p.Select(new[]{C(a,2),C(b,1)},0);
            p.Select(new[]{C(a,.5f),C(b,1)},1);p.Select(new[]{C(a,2),C(b,1)},2);
            Same(a,p.Select(new[]{C(a,2),C(b,1)},3));Same(b,p.Select(new[]{C(a,2),C(b,1)},4));
        });
        Run("despawn_or_inactive_removal_is_immediate", () => {
            var a=new AttentionActor();var b=new AttentionActor();var p=new Policy();
            p.Select(new[]{C(a,1),C(b,2)},0);Same(b,p.Select(new[]{C(b,2)},.1));
            Same(null,p.Select(Array.Empty<Policy.Candidate>(),.2));
        });
        Run("radius_2m_inclusive_outside_fallback", () => {
            var a=new AttentionActor();var p=new Policy();
            Same(a,p.Select(new[]{C(a,4)},0));Same(null,p.Select(new[]{C(a,4.0001f)},.1));
            Same(a,p.Select(new[]{C(a,4)},.2));
        });
        Run("range_exit_replaces_valid_immediately", () => {
            var a=new AttentionActor();var b=new AttentionActor();var p=new Policy();
            p.Select(new[]{C(a,1),C(b,2)},0);Same(b,p.Select(new[]{C(a,4.01f),C(b,2)},.1));
        });
        Run("front_lateral_boundary_outside_behind", () => {
            Check(Policy.IsEligible(1,1));Check(Policy.IsEligible(1,.6f));Check(Policy.IsEligible(1,.5f));
            Check(!Policy.IsEligible(1,.499f));Check(!Policy.IsEligible(1,0));Check(!Policy.IsEligible(1,-1));
        });
        Run("cone_exit_immediate_replace_or_fallback", () => {
            var a=new AttentionActor();var b=new AttentionActor();var p=new Policy();
            p.Select(new[]{C(a,1),C(b,2)},0);Same(b,p.Select(new[]{C(a,1,.49f),C(b,2)},.1));
            Same(null,p.Select(new[]{C(a,1,-1),C(b,2,0)},.2));
        });
        Run("ineligible_challenger_time_is_not_counted", () => {
            var a=new AttentionActor();var b=new AttentionActor();var p=new Policy();
            p.Select(new[]{C(a,2)},0);p.Select(new[]{C(a,2),C(b,1,.49f)},0);
            Same(a,p.Select(new[]{C(a,2),C(b,1,.6f)},3));
            Same(a,p.Select(new[]{C(a,2),C(b,1,.6f)},4.9));Same(b,p.Select(new[]{C(a,2),C(b,1,.6f)},5));
        });
        Run("reset_round_drops_old_instance_and_timer", () => {
            var a=new AttentionActor();var b=new AttentionActor();var fresh=new AttentionActor();var p=new Policy();
            p.Select(new[]{C(a,2)},0);p.Select(new[]{C(a,2),C(b,1)},0);p.Reset();
            Same(null,p.Current);Same(fresh,p.Select(new[]{C(fresh,3)},1.9));
        });
        Run("species_agnostic_for_humans_and_mosquitoes", () => {
            foreach(var role in new[]{"Human","Mosquito"}) {
                var a=new AttentionActor(role);var b=new AttentionActor(role=="Human"?"Mosquito":"Human");var p=new Policy();
                Same(b,p.Select(new[]{C(a,3),C(b,1)},0)); // No role/team filtering exists in policy.
            }
        });
        Run("invalid_geometry_is_not_selected", () => {
            var a=new AttentionActor();var p=new Policy();
            Same(null,p.Select(new[]{C(a,float.NaN),C(a,-1),C(a,float.PositiveInfinity),C(a,1,float.NaN)},0));
        });
        Run("clock_rewind_restarts_challenger", () => {
            var a=new AttentionActor();var b=new AttentionActor();var p=new Policy();
            p.Select(new[]{C(a,2)},10);p.Select(new[]{C(a,2),C(b,1)},10);
            Same(a,p.Select(new[]{C(a,2),C(b,1)},1));Same(a,p.Select(new[]{C(a,2),C(b,1)},2.9));
            Same(b,p.Select(new[]{C(a,2),C(b,1)},3));
        });
        Console.WriteLine("NEAREST_ATTENTION_CPU cases="+cases+" failures="+failures+" native=false");
        return failures==0?0:1;
    }
}


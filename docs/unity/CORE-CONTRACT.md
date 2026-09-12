# Core contract, alfa 1
Pure C# assembly LetMeSleep.Core, no UnityEngine. Source unity/Assets/LetMeSleep/Core/RoomSession.cs.
RoomSession owns waiting/playing/results/closed state and fresh immutable snapshots.
Transport authenticates sender IDs; UI uses RoomError for localized messaging.
RoomSession.Protocol lms-unity-094-alfa-2 must match exact version in alfa. Different engine versions never connect by reusing the Godot code prefix.
Rules: human count nullable (automatic),1..5 if fixed;bothteamsrequired;16total;house-patio-v1only.
SetReady changes only sender; changedrules reset ready; onlyowner changesrules/starts/finishes/returns.
Join waitingonly; midroundjoin rejected with clear message initially. Hostleavecloses. GuestdisconnectnotifiesGameplay which owns outcome.
Fisher-Yates per round; role repeats are allowed by genuine random draw, never promise everyone switches.
Round rules defaults currently tuning proposal:180s,quota20. These are not balance claims.
Next: transport DTO codec, lobby invite lookup and host-authoritative simulation integration. Do not serialize domain property objects by assuming Unity JsonUtility supports them; use explicit bounded DTOs.



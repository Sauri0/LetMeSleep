# Core contract, alfa 1
Pure C# assembly LetMeSleep.Core, no UnityEngine. Source unity/Assets/LetMeSleep/Core/RoomSession.cs.
RoomSession owns waiting/playing/results/closed state and fresh immutable snapshots.
Transport authenticates sender IDs; UI uses RoomError for localized messaging.
RoomSession.Protocol lms-unity-094-alfa-2 must match exact version in alfa. Different engine versions never connect by reusing the Godot code prefix.
Rules: human count nullable (automatic), 1..5 if fixed; both teams required; 16 total. The default remains `house-patio-v1`. RoomRules accepts that map plus the five explicit Higgsfield IDs: `hf-isla-del-laguito-v2`, `hf-casa-del-patio-v1`, `hf-campamento-pinar-v2`, `hf-yate-a-la-deriva-v3`, `hf-puerto-del-faro-v1`. Unknown IDs and incomplete revisions remain invalid. Bootstrap must also have the selected map installed in its catalog; the allowlist alone is not a content installation or WAN certification.
SetReady changes only sender; changedrules reset ready; onlyowner changesrules/starts/finishes/returns.
Join waitingonly; midroundjoin rejected with clear message initially. Hostleavecloses. GuestdisconnectnotifiesGameplay which owns outcome.
Fisher-Yates per round; role repeats are allowed by genuine random draw, never promise everyone switches.
Round rules defaults currently tuning proposal:180s,quota20. These are not balance claims.
Next: transport DTO codec, lobby invite lookup and host-authoritative simulation integration. Do not serialize domain property objects by assuming Unity JsonUtility supports them; use explicit bounded DTOs.



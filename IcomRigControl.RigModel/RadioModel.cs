namespace IcomRigControl.RigModel;

/// <summary>
/// Identifies which physical radio model this Transceiver instance targets.
/// Determines the CI-V address. All three supported radios share the same core
/// CI-V command/sub-command set (frequency, mode, PTT, meters, scope, memory);
/// see CLAUDE.md "Supported Radios — Scope Decision".
/// </summary>
public enum RadioModel
{
    IC7300,
    IC7300MK2,
    IC705
}

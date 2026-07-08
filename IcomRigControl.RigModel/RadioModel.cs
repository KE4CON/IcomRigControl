namespace IcomRigControl.RigModel;

/// <summary>
/// Identifies which physical radio model this Transceiver instance targets.
/// Determines CI-V address and which MK2-only commands are available.
/// </summary>
public enum RadioModel
{
    IC7300,
    IC7300MK2
}

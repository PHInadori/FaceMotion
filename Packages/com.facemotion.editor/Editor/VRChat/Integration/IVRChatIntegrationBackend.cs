namespace FaceMotion.Editor.VRChat.Integration
{
    public interface IVRChatIntegrationBackend
    {
        string BackendId { get; }

        int BackendVersion { get; }
    }
}

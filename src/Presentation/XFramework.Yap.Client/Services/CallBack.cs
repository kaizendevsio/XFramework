namespace Yap.Client.Services;

/// <summary>Calling a conversation back from its history, shared by the Calls tab and the in-thread call events.</summary>
public static class CallBack
{
    public static bool Available(ChatState state, VoiceState voice) => !voice.IsCalling && state.Online && state.SessionReady;

    public static async Task StartAsync(ChatState state, VoiceState voice, Guid thread, bool video)
    {
        try
        {
            await voice.InitializeAsync();
            var conversation = await state.ConversationDetailsAsync(thread);
            await voice.StartGroupAsync(conversation.Id, conversation.Name, conversation.People, video);
        }
        catch (Exception ex) { state.Report(ex); }
    }
}

using NUnit.Framework;
using Yap.Contracts;

namespace Yap.Client.Tests;

/// <summary>
/// The composer shows a photo, a video poster or a voice player instead of a name-and-size
/// chip, so the classification that picks between them has to agree with what can render.
/// </summary>
public sealed class AttachmentPreviewTests
{
    [TestCase("image/jpeg", "holiday.jpg", AttachmentPreview.Photo)]
    [TestCase("image/png", "shot.png", AttachmentPreview.Photo)]
    [TestCase("application/octet-stream", "IMG_0042.HEIC", AttachmentPreview.Photo)]
    [TestCase("video/mp4", "clip.mp4", AttachmentPreview.Video)]
    [TestCase("application/octet-stream", "IMG_0042.MOV", AttachmentPreview.Video)]
    [TestCase("audio/webm;codecs=opus", "voice.webm", AttachmentPreview.Voice)]
    [TestCase("", "note.m4a", AttachmentPreview.Voice)]
    [TestCase("application/pdf", "invoice.pdf", AttachmentPreview.File)]
    [TestCase("image/svg+xml", "logo.svg", AttachmentPreview.File)]
    [TestCase("audio/flac", "master.flac", AttachmentPreview.File)]
    [TestCase("application/octet-stream", "archive.zip", AttachmentPreview.File)]
    public void Preview_MatchesWhatCanRender(string type, string name, AttachmentPreview expected) =>
        Assert.That(ChatMedia.Preview(type, name), Is.EqualTo(expected));

    [Test]
    public void Preview_OnlyFileKindHasNothingToShow()
    {
        // Every non-file kind must be previewable, or removing the chip would leave a blank row.
        foreach (var (type, name) in new[] { ("image/webp", "a.webp"), ("video/quicktime", "a.mov"), ("audio/mp4", "a.m4a") })
        {
            var kind = ChatMedia.Preview(type, name);
            Assert.That(ChatMedia.IsInlineImage(type, name) || ChatMedia.IsInlineVideo(type, name) || ChatMedia.IsInlineAudio(type, name),
                Is.True, $"{name} classified as {kind} but nothing can render it");
        }
    }
}

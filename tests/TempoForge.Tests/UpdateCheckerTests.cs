using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using TempoForge.Core.Updates;

namespace TempoForge.Tests;

public sealed class UpdateCheckerTests
{
    private const string Prefix = "https://github.com/erichs890/TempoForge/releases/download/";

    private static string Release(string tag, string assetUrl, string name = "TempoForge-setup.exe",
        long size = 5, string? digest = null, bool prerelease = false) =>
        $$"""
        {"tag_name":"{{tag}}","draft":false,"prerelease":{{(prerelease ? "true" : "false")}},
         "assets":[{"name":"notas.txt","browser_download_url":"{{Prefix}}{{tag}}/notas.txt","size":1},
                   {"name":"{{name}}","browser_download_url":"{{assetUrl}}","size":{{size}}
                    {{(digest == null ? "" : $",\"digest\":\"{digest}\"")}}}]}
        """;

    [Theory]
    [InlineData("v0.2.0", "0.1.0", true)]
    [InlineData("0.2", "0.1.9", true)]
    [InlineData("v0.1.0", "0.1.0", false)]
    [InlineData("v0.1.0", "0.2.0", false)]
    [InlineData("v1.0.0-beta", "0.9.0", true)]
    [InlineData("nao-e-versao", "0.1.0", false)]
    public void CompareVersions(string tag, string current, bool expectUpdate)
    {
        UpdateInfo? info = UpdateChecker.Parse(Release(tag, Prefix + tag + "/x-setup.exe"), Version.Parse(current), Prefix);
        Assert.Equal(expectUpdate, info != null);
    }

    [Theory]
    [InlineData("exe", "TempoForge-0.2.0-setup.exe", "0.1.0", true)]   // tag que não é versão
    [InlineData("exe", "TempoForge-0.1.0-setup.exe", "0.1.0", false)]
    [InlineData("v9.0.0", "TempoForge-0.1.0-setup.exe", "0.1.0", false)] // o nome do arquivo manda
    [InlineData("exe", "TempoForge-setup.exe", "0.1.0", false)]
    public void VersionComesFromInstallerName(string tag, string name, string current, bool expectUpdate)
    {
        UpdateInfo? info = UpdateChecker.Parse(Release(tag, Prefix + tag + "/" + name, name), Version.Parse(current), Prefix);
        Assert.Equal(expectUpdate, info != null);
    }

    [Fact]
    public void IgnoresCurrentVersionWithFourParts()
    {
        // A versão do assembly vem como 0.2.0.0; não pode parecer mais antiga que a tag v0.2.0.
        Assert.Null(UpdateChecker.Parse(Release("v0.2.0", Prefix + "v0.2.0/x-setup.exe"), new Version(0, 2, 0, 0), Prefix));
    }

    [Fact]
    public void IgnoresPrereleaseAndForeignHosts()
    {
        var current = new Version(0, 1, 0);
        Assert.Null(UpdateChecker.Parse(Release("v0.2.0", Prefix + "v0.2.0/x-setup.exe", prerelease: true), current, Prefix));
        Assert.Null(UpdateChecker.Parse(Release("v0.2.0", "https://exemplo.invalid/x-setup.exe"), current, Prefix));
        Assert.Null(UpdateChecker.Parse(Release("v0.2.0", "http://github.com/erichs890/TempoForge/releases/download/v0.2.0/x-setup.exe"), current, Prefix));
    }

    [Fact]
    public async Task NetworkFailureMeansNoUpdate()
    {
        var checker = new UpdateChecker(new FakeHandler(_ => throw new HttpRequestException("sem rede")));
        Assert.Null(await checker.CheckAsync(new Version(0, 1, 0)));

        checker = new UpdateChecker(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        Assert.Null(await checker.CheckAsync(new Version(0, 1, 0)));
    }

    [Fact]
    public async Task DownloadsAndVerifiesInstaller()
    {
        byte[] payload = [1, 2, 3, 4, 5];
        string sha = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        string url = Prefix + "v0.2.0/TempoForge-0.2.0-setup.exe";
        var handler = new FakeHandler(request => request.RequestUri!.Host == "api.github.com"
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Release("v0.2.0", url, "TempoForge-0.2.0-setup.exe", 5, "sha256:" + sha)) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) });
        var checker = new UpdateChecker(handler);
        string folder = Path.Combine(Path.GetTempPath(), $"tempoforge-upd-{Guid.NewGuid():N}");
        try
        {
            UpdateInfo? info = await checker.CheckAsync(new Version(0, 1, 0));
            Assert.NotNull(info);
            Assert.Equal(new Version(0, 2, 0), info!.Version);

            string path = await checker.DownloadAsync(info, folder);
            Assert.Equal(payload, File.ReadAllBytes(path));

            // Conteúdo adulterado: o arquivo não pode ficar no disco.
            UpdateInfo tampered = info with { Sha256 = new string('0', 64), FileName = "outro-setup.exe" };
            await Assert.ThrowsAsync<InvalidDataException>(() => checker.DownloadAsync(tampered, folder));
            Assert.False(File.Exists(Path.Combine(folder, "outro-setup.exe")));
            Assert.Empty(Directory.GetFiles(folder, "*.parcial"));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancel) =>
            Task.FromResult(respond(request));
    }
}

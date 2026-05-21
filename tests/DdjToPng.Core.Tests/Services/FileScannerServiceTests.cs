using DdjToPng.Core.Services;

namespace DdjToPng.Core.Tests.Services;

public sealed class FileScannerServiceTests : IDisposable
{
    private readonly FileScannerService _sut = new();
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public FileScannerServiceTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void FindDdjFiles_Should_Return_Only_Ddj_Files()
    {
        CreateFile("icon1.ddj");
        CreateFile("icon2.ddj");
        CreateFile("readme.txt");

        var result = _sut.FindDdjFiles(_tempDir);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(f => f.Should().EndWith(".ddj"));
    }

    [Fact]
    public void FindDdjFiles_Should_Return_Empty_When_No_Ddj_Files_Exist()
    {
        CreateFile("readme.txt");

        var result = _sut.FindDdjFiles(_tempDir);

        result.Should().BeEmpty();
    }

    [Fact]
    public void FindDdjFiles_Should_Not_Search_Subdirectories_When_Recursive_False()
    {
        var subDir = Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));
        File.WriteAllBytes(Path.Combine(subDir.FullName, "nested.ddj"), []);

        var result = _sut.FindDdjFiles(_tempDir, recursive: false);

        result.Should().BeEmpty();
    }

    [Fact]
    public void FindDdjFiles_Should_Search_Subdirectories_When_Recursive_True()
    {
        var subDir = Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));
        File.WriteAllBytes(Path.Combine(subDir.FullName, "nested.ddj"), []);

        var result = _sut.FindDdjFiles(_tempDir, recursive: true);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void FindDdjFiles_Should_Throw_DirectoryNotFoundException_When_Directory_Missing()
    {
        var act = () => _sut.FindDdjFiles(@"C:\this_path_does_not_exist_xyz");
        act.Should().Throw<DirectoryNotFoundException>();
    }

    private void CreateFile(string name) =>
        File.WriteAllBytes(Path.Combine(_tempDir, name), []);
}

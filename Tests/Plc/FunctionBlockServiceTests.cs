using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Models.Architecture;
using MOCHA.Services.Architecture;

namespace Tests.Plc;

[TestClass]
public class FunctionBlockServiceTests
{
    [TestMethod]
    public async Task ファンクションブロック追加_正しい入力_登録される()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mocha_fb_{Guid.NewGuid():N}");
        try
        {
            var options = Options.Create(new PlcStorageOptions { RootPath = root });
            var pathBuilder = new PlcFileStoragePathBuilder(options);
            var repository = new InMemoryPlcUnitRepository();
            var service = new FunctionBlockService(repository, pathBuilder, NullLogger<FunctionBlockService>.Instance);

            var unitDraft = new PlcUnitDraft
            {
                Name = "ユニットA",
                Manufacturer = PlcUnitDraft.SupportedManufacturers.First(),
                ProgramFiles = Array.Empty<PlcFileUpload>(),
                Modules = Array.Empty<PlcModuleDraft>()
            };
            var unit = PlcUnit.Create("user1", "001", unitDraft);
            await repository.AddAsync(unit);

            var labelBytes = Encoding.UTF8.GetBytes("device,comment\nX0,スタート");
            var programBytes = Encoding.UTF8.GetBytes("line,instruction\n0000,LD X0");
            var fbDraft = new FunctionBlockDraft
            {
                Name = "StartLogic",
                LabelFile = new PlcFileUpload { FileName = "label.csv", Content = labelBytes, ContentType = "text/csv", FileSize = labelBytes.LongLength },
                ProgramFile = new PlcFileUpload { FileName = "program.csv", Content = programBytes, ContentType = "text/csv", FileSize = programBytes.LongLength }
            };

            var result = await service.AddAsync("user1", "001", unit.Id, fbDraft);

            Assert.IsTrue(result.Succeeded, result.Error);
            Assert.IsNotNull(result.Value);
            var storedUnit = await repository.GetAsync(unit.Id);
            Assert.IsNotNull(storedUnit);
            Assert.AreEqual(1, storedUnit!.FunctionBlocks.Count);
            var fb = storedUnit.FunctionBlocks.Single();
            Assert.AreEqual("StartLogic", fb.Name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(fb.LabelFile.RelativePath));
            Assert.IsFalse(string.IsNullOrWhiteSpace(fb.ProgramFile.RelativePath));
            Assert.IsTrue(File.Exists(Path.Combine(fb.LabelFile.StorageRoot!, fb.LabelFile.RelativePath!)));
            Assert.IsTrue(File.Exists(Path.Combine(fb.ProgramFile.StorageRoot!, fb.ProgramFile.RelativePath!)));

            var contents = service.ReadContents(fb);
            StringAssert.Contains(contents.Label, "スタート");
            StringAssert.Contains(contents.Program, "LD X0");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

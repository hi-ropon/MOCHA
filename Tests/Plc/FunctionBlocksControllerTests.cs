using System;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Controllers;
using MOCHA.Models.Architecture;
using MOCHA.Services.Architecture;

namespace Tests.Plc;

[TestClass]
public class FunctionBlocksControllerTests
{
    [TestMethod]
    public async Task アップロード_正しい入力_Createdが返る()
    {
        var root = Path.Combine(Path.GetTempPath(), $"fb_api_{Guid.NewGuid():N}");
        try
        {
            var pathBuilder = new PlcFileStoragePathBuilder(Options.Create(new PlcStorageOptions { RootPath = root }));
            var repo = new InMemoryPlcUnitRepository();
            var service = new FunctionBlockService(repo, pathBuilder, NullLogger<FunctionBlockService>.Instance);
            var controller = new FunctionBlocksController(service);
            var userId = "user-api";
            var agentNumber = "001";
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, userId)
                    }, "test"))
                }
            };

            var unitDraft = new PlcUnitDraft
            {
                Name = "ユニット",
                Manufacturer = PlcUnitDraft.SupportedManufacturers[0],
                GatewayHost = "127.0.0.1",
                GatewayPort = 8000
            };
            var unit = PlcUnit.Create(userId, agentNumber, unitDraft);
            await repo.AddAsync(unit);

            var labelBytes = Encoding.UTF8.GetBytes("device,comment\nX0,スタート");
            var programBytes = Encoding.UTF8.GetBytes("line,instruction\n0,LD X0");
            var request = new FunctionBlockUploadRequest
            {
                AgentNumber = agentNumber,
                Name = "Start",
                LabelFile = new FormFile(new MemoryStream(labelBytes), 0, labelBytes.Length, "label", "label.csv"),
                ProgramFile = new FormFile(new MemoryStream(programBytes), 0, programBytes.Length, "program", "program.csv")
            };

            var result = await controller.UploadAsync(unit.Id, request);

            Assert.IsInstanceOfType(result, typeof(CreatedAtActionResult));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}

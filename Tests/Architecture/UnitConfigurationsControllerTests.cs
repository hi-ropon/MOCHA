using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MOCHA.Controllers;
using MOCHA.Models.Architecture;
using MOCHA.Services.Architecture;

namespace MOCHA.Tests;

/// <summary>
/// UnitConfigurationsController の動作確認
/// </summary>
[TestClass]
public class UnitConfigurationsControllerTests
{
    /// <summary>
    /// 正常追加で Created が返ることを確認
    /// </summary>
    [TestMethod]
    public async Task AddAsync_正常入力_Createdが返る()
    {
        var controller = CreateController(out _);

        var request = new UnitConfigurationRequest
        {
            AgentNumber = "A-10",
            Name = "ユニット10",
            Description = "説明",
            Devices = new List<UnitDeviceRequest> { new() { Name = "搬送機" } }
        };

        var result = await controller.AddAsync(request);

        var created = result as CreatedAtActionResult;
        Assert.IsNotNull(created);
        var response = created.Value as UnitConfigurationResponse;
        Assert.IsNotNull(response);
        Assert.AreEqual("ユニット10", response!.Name);
        Assert.AreEqual(1, response.Devices.Count);
    }

    /// <summary>
    /// 既存更新で Ok が返ることを確認
    /// </summary>
    [TestMethod]
    public async Task UpdateAsync_既存更新_Okで上書きされる()
    {
        var controller = CreateController(out var service);
        var created = await service.AddAsync(
            "user-controller",
            "A-20",
            new UnitConfigurationDraft
            {
                Name = "ユニット20",
                Description = "旧説明",
                Devices = new List<UnitDeviceDraft> { new() { Name = "旧機器" } }
            });

        var request = new UnitConfigurationRequest
        {
            AgentNumber = "A-20",
            Name = "ユニット20-更新",
            Description = "新説明",
            Devices = new List<UnitDeviceRequest>
            {
                new() { Name = "新機器1", Model = "M-1" },
                new() { Name = "新機器2" }
            }
        };

        var result = await controller.UpdateAsync(created.Unit!.Id, request);

        var ok = result as OkObjectResult;
        Assert.IsNotNull(ok);
        var response = ok.Value as UnitConfigurationResponse;
        Assert.IsNotNull(response);
        Assert.AreEqual("ユニット20-更新", response!.Name);
        Assert.AreEqual("新説明", response.Description);
        Assert.AreEqual(2, response.Devices.Count);
    }

    /// <summary>
    /// 削除で NoContent が返ることを確認
    /// </summary>
    [TestMethod]
    public async Task DeleteAsync_既存削除_NoContentが返る()
    {
        var controller = CreateController(out var service);
        var created = await service.AddAsync(
            "user-controller",
            "A-30",
            new UnitConfigurationDraft
            {
                Name = "ユニット30",
                Devices = new List<UnitDeviceDraft> { new() { Name = "搬送機" } }
            });

        var result = await controller.DeleteAsync(created.Unit!.Id, "A-30");

        Assert.IsInstanceOfType(result, typeof(NoContentResult));
    }

    /// <summary>
    /// 一覧取得で登録済みが返ることを確認
    /// </summary>
    [TestMethod]
    public async Task ListAsync_登録済み取得_一覧が返る()
    {
        var controller = CreateController(out var service);
        await service.AddAsync(
            "user-controller",
            "A-40",
            new UnitConfigurationDraft
            {
                Name = "ユニット40-1",
                Devices = new List<UnitDeviceDraft> { new() { Name = "搬送機" } }
            });
        await service.AddAsync(
            "user-controller",
            "A-40",
            new UnitConfigurationDraft
            {
                Name = "ユニット40-2",
                Devices = new List<UnitDeviceDraft> { new() { Name = "検査機" } }
            });

        var result = await controller.ListAsync("A-40");

        var ok = result.Result as OkObjectResult;
        Assert.IsNotNull(ok);
        var list = ok.Value as IReadOnlyList<UnitConfigurationResponse>;
        Assert.IsNotNull(list);
        Assert.AreEqual(2, list!.Count);
    }

    private static UnitConfigurationsController CreateController(out UnitConfigurationService service)
    {
        var repository = new InMemoryUnitConfigurationRepository();
        service = new UnitConfigurationService(repository, NullLogger<UnitConfigurationService>.Instance);
        var controller = new UnitConfigurationsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "user-controller")
                    }, "test"))
                }
            }
        };
        return controller;
    }
}

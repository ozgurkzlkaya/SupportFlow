using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SupportFlow.Api.Controllers;

namespace SupportFlow.Api.Tests.Tickets;

public sealed class TicketsControllerAttachmentTests
{
    [Fact]
    public void UpdatePriority_AllowsOnlyAdminAndTechnicalStaff()
    {
        var method = typeof(TicketsController).GetMethod(nameof(TicketsController.UpdatePriority));
        Assert.NotNull(method);

        var attribute = Assert.Single(method!.GetCustomAttributes<AuthorizeAttribute>());

        Assert.Equal("TechnicalStaff,Admin", attribute.Roles);
    }

    [Fact]
    public async Task GetAttachments_WithoutAuthenticatedUser_ReturnsUnauthorized()
    {
        var controller = CreateController();

        var result = await controller.GetAttachments(1009, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetTimeline_WithoutAuthenticatedUser_ReturnsUnauthorized()
    {
        var controller = CreateController();

        var result = await controller.GetTimeline(1009, 1, 25, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task GetTimeline_RejectsInvalidPagination(int page, int pageSize)
    {
        var controller = CreateAuthenticatedController();

        var result = await controller.GetTimeline(1009, page, pageSize, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UploadAttachment_WithoutAuthenticatedUser_ReturnsUnauthorized()
    {
        var controller = CreateController();

        var result = await controller.UploadAttachment(
            1009,
            null,
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public void UploadAttachment_UsesMultipartFormFieldNamedFile()
    {
        var method = typeof(TicketsController).GetMethod(nameof(TicketsController.UploadAttachment));
        Assert.NotNull(method);

        var fileParameter = method!
            .GetParameters()
            .Single(parameter => parameter.ParameterType == typeof(IFormFile));

        Assert.Equal("file", fileParameter.Name);
        Assert.Null(fileParameter.GetCustomAttribute<FromFormAttribute>());
        Assert.Contains(
            method!.GetCustomAttributes<ConsumesAttribute>(),
            attribute => attribute.ContentTypes.Contains("multipart/form-data"));
    }

    [Fact]
    public async Task GetAll_RejectsInvalidAssignmentFilter()
    {
        var controller = CreateAuthenticatedController();

        var result = await controller.GetAll(
            null,
            null,
            null,
            (SupportFlow.Api.Enums.TicketAssignmentFilter)999,
            false,
            null,
            null,
            null,
            1,
            25,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetAll_RejectsContradictoryAssignedUserAndAssignmentState()
    {
        var controller = CreateAuthenticatedController();

        var result = await controller.GetAll(
            null,
            null,
            1009,
            SupportFlow.Api.Enums.TicketAssignmentFilter.Unassigned,
            true,
            null,
            null,
            null,
            1,
            25,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task GetAll_RejectsInvalidPagination(int page, int pageSize)
    {
        var controller = CreateAuthenticatedController();

        var result = await controller.GetAll(
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            page,
            pageSize,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    private static TicketsController CreateController()
    {
        return new TicketsController(null!, null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };
    }

    private static TicketsController CreateAuthenticatedController()
    {
        var controller = CreateController();
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "1009"),
                new Claim(ClaimTypes.Role, "Admin")
            ],
            "Test"));
        return controller;
    }
}

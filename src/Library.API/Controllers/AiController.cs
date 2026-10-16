using Library.Application.Common;
using Library.Application.DTOs.Common;
using Library.Application.Interfaces;
using Library.Domain.Dtos;
using Library.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly IBookService _bookService;
    private readonly ILogger<AiController> _logger;

    public AiController(
        IAiService aiService,
        IBookService bookService,
        ILogger<AiController> logger)
    {
        _aiService = aiService;
        _bookService = bookService;
        _logger = logger;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<ApiResponse<object>>> Ask(
        [FromBody] AiAskRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(ApiResponse<object>.Fail("السؤال مطلوب."));

        try
        {
            // 1) ابحث في مكتبتنا عن كتب متعلقة بالسؤال
            var searchResult = await _bookService.SearchAsync(
                request.Question,
                new PaginationQuery { Page = 1, PageSize = 20 },
                ct);

            var candidates = searchResult.Items.Select(b => new AiBookCandidate
            {
                Title = b.Title,
                Author = b.Author,
                Key = b.Id.ToString(),
            }).ToList();

            // 2) اطلب من Gemini النصيحة بناء على الكتب الحقيقية
            var answer = await _aiService.GetBookAdviceAsync(
                request.Question,
                candidates,
                ct);

            return Ok(ApiResponse<object>.Ok(new
            {
                answer,
                suggestedBooks = searchResult.Items
            }, "تم"));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "AI service error");
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }
}

public class AiAskRequest
{
    public string Question { get; set; } = string.Empty;
}
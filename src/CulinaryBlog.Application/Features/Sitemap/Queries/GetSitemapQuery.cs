using CulinaryBlog.Application.Common.Interfaces;
using MediatR;

namespace CulinaryBlog.Application.Features.Sitemap.Queries;

/// <summary>
/// FR-JOB-003 (Tuần 5 — Tiến): Query lấy sitemap XML phục vụ endpoint GET /sitemap.xml.
/// </summary>
public record GetSitemapQuery : IRequest<string>;

public class GetSitemapQueryHandler(ISitemapService sitemapService) : IRequestHandler<GetSitemapQuery, string>
{
    public async Task<string> Handle(GetSitemapQuery request, CancellationToken ct)
    {
        return await sitemapService.GetSitemapXmlAsync(ct);
    }
}

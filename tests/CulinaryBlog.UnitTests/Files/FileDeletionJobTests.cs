using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Features.Files.Commands.DeleteFile;
using CulinaryBlog.Infrastructure.Jobs;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CulinaryBlog.UnitTests.Files;

/// <summary>FR-FILE-002 (Thịnh) — xoá file MinIO qua Hangfire job, retry 3 lần.</summary>
public class FileDeletionJobTests
{
    private sealed class RecordingJobClient : IBackgroundJobClient
    {
        public List<Job> Jobs { get; } = [];

        public string Create(Job job, IState state)
        {
            Jobs.Add(job);
            return Jobs.Count.ToString();
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }

    private sealed class RecordingSender : ISender
    {
        public List<object> Sent { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.FromResult(default(TResponse)!);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            Sent.Add(request!);
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            Sent.Add(request);
            return Task.FromResult<object?>(null);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    [Fact]
    public void Scheduler_ShouldEnqueueOneDeleteFileJobPerDistinctObject()
    {
        var client = new RecordingJobClient();
        var scheduler = new HangfireFileDeletionScheduler(client);

        var count = scheduler.Enqueue(["recipes/a.jpg", "recipes/a.jpg", " ", "recipes/a_thumb.jpg"]);

        Assert.Equal(2, count);
        Assert.All(client.Jobs, job =>
        {
            Assert.Equal(typeof(DeleteFileJob), job.Type);
            Assert.Equal(nameof(DeleteFileJob.ExecuteAsync), job.Method.Name);
        });
        Assert.Equal(["recipes/a.jpg", "recipes/a_thumb.jpg"], client.Jobs.Select(job => (string)job.Args[0]));
    }

    [Fact]
    public void DeleteFileJob_ShouldRetryThreeTimes_PerSrs()
    {
        var retry = typeof(DeleteFileJob).GetMethod(nameof(DeleteFileJob.ExecuteAsync))!
            .GetCustomAttributes(typeof(AutomaticRetryAttribute), false)
            .Cast<AutomaticRetryAttribute>()
            .Single();

        Assert.Equal(3, retry.Attempts);
    }

    [Fact]
    public async Task DeleteFileJob_ShouldSendDeleteFileCommand()
    {
        var sender = new RecordingSender();
        var job = new DeleteFileJob(sender, NullLogger<DeleteFileJob>.Instance);

        await job.ExecuteAsync("recipes/pho.jpg");

        var command = Assert.IsType<DeleteFileCommand>(Assert.Single(sender.Sent));
        Assert.Equal("recipes/pho.jpg", command.ObjectName);
    }

    [Fact]
    public void Resolver_ShouldCollectAllImageVersions_AndSkipForeignUrls()
    {
        var names = FileStorageObjectNameResolver.FromImageUrls(
            "recipe-images",
            "http://localhost:9000/recipe-images/uploads/2026/10/a.png",
            "http://localhost:9000/recipe-images/uploads/2026/10/a_medium.jpg",
            null,
            "https://example.com/someone-else/photo.jpg",
            "http://localhost:9000/recipe-images/uploads/2026/10/a.png");

        Assert.Equal(["uploads/2026/10/a.png", "uploads/2026/10/a_medium.jpg"], names);
    }
}

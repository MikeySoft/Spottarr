using System.Data.Common;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spottarr.Data;
using Spottarr.Data.Entities;
using Spottarr.Services.Configuration;
using Spottarr.Services.Contracts;
using Spottarr.Services.Helpers;
using Spottarr.Services.Nntp;
using Spottarr.Services.Parsers;
using Usenet.Nntp.Models;

namespace Spottarr.Services;

internal sealed class SpotnetService : ISpotnetService
{
    private const int BatchSize = 1000;
    private readonly ILogger<SpotnetService> _logger;
    private readonly IOptions<UsenetOptions> _usenetOptions;
    private readonly IOptions<SpotnetOptions> _spotnetOptions;
    private readonly SpottarrDbContext _dbContext;

    public SpotnetService(ILoggerFactory loggerFactory, ILogger<SpotnetService> logger,
        IOptions<UsenetOptions> usenetOptions, IOptions<SpotnetOptions> spotnetOptions, SpottarrDbContext dbContext)
    {
        _logger = logger;
        _usenetOptions = usenetOptions;
        _spotnetOptions = spotnetOptions;
        _dbContext = dbContext;

        // Enable NNTP client logging
        Usenet.Logger.Factory = loggerFactory;
    }

    public async Task Import()
    {
        using var handler = new NntpClientHandler(_usenetOptions.Value);
        await handler.ConnectAsync();

        var spotnetOptions = _spotnetOptions.Value;
        var groupResponse = handler.Client.Group(spotnetOptions.SpotGroup);

        if (!groupResponse.Success)
        {
            _logger.CouldNotRetrieveSpotGroup(spotnetOptions.SpotGroup, groupResponse.Code, groupResponse.Message);
            return;
        }

        var group = groupResponse.Group;
        var batches = GetBatches(group.LowWaterMark, group.HighWaterMark, spotnetOptions.RetrieveCount).ToList();

        var retrieveAfterUtc = spotnetOptions.RetrieveAfter.UtcDateTime;
        var existing = await _dbContext.Spots
            .Where(s => s.CreatedAt >= retrieveAfterUtc)
            .Select(s => s.MessageId)
            .ToHashSetAsync();
        
        var context = new SpotImportResult(existing);
        
        foreach (var batch in batches)
        {
            var done = ImportBatch(context, handler, batch, spotnetOptions.RetrieveAfter);
            if (done) break;
        }

        try
        {
            await _dbContext.BulkInsertOrUpdateAsync(context.ImageSpots, ConfigureBulkInsert);
            await _dbContext.BulkInsertOrUpdateAsync(context.AudioSpots, ConfigureBulkInsert);
            await _dbContext.BulkInsertOrUpdateAsync(context.GameSpots, ConfigureBulkInsert);
            await _dbContext.BulkInsertOrUpdateAsync(context.ApplicationSpots, ConfigureBulkInsert);
        }
        catch (DbException ex)
        {
            _logger.FailedToSaveSpots(ex);
        }
    }

    private void ConfigureBulkInsert(BulkConfig config)
    {
        config.UpdateByProperties = [nameof(Spot.MessageId)];
        config.PropertiesToIncludeOnUpdate = [ string.Empty ];
    }

    private bool ImportBatch(SpotImportResult context, NntpClientHandler handler, NntpArticleRange batch,
        DateTimeOffset retrieveAfter)
    {
        var xOverResponse = handler.Client.Xover(batch);
        if (!xOverResponse.Success)
        {
            _logger.CouldNotRetrieveArticles(batch.From, batch.To, xOverResponse.Code, xOverResponse.Message);
            return true;
        }

        foreach (var header in xOverResponse.Lines)
        {
            try
            {
                var nntpHeader = NntpHeaderParser.Parse(header);

                if (nntpHeader.Date < retrieveAfter)
                {
                    _logger.ReachedRetrieveAfter(retrieveAfter);
                    return true;
                }
                
                var spotnetHeader = SpotnetHeaderParser.Parse(nntpHeader);

                var spot = spotnetHeader.ToSpot();
                context.AddSpot(spot);
            }
            catch (ArgumentException ex)
            {
                _logger.FailedToParseSpotHeader(ex);
            }
        }

        return false;
    }

    private static IEnumerable<NntpArticleRange> GetBatches(long lowWaterMark, long highWaterMark, int retrieveCount)
    {
        var start = retrieveCount > 0 ? highWaterMark - retrieveCount : lowWaterMark;
        var batchEnd = highWaterMark;
        
        while (batchEnd >= start)
        {
            var batchStart = Math.Max(start, batchEnd - (BatchSize - 1));
            
            // Make sure that the final batch is inclusive
            if (batchStart - 1 == start) batchStart = start;

            yield return new NntpArticleRange(batchStart, batchEnd);
            
            batchEnd = batchStart - 1;
        }
    }
}
using Microsoft.EntityFrameworkCore;
using SaaSForge.Api.Data;
using SaaSForge.Api.Models;
using SaaSForge.Api.Modules.Leads.Constants;
using SaaSForge.Api.Modules.Leads.Dtos;
using SaaSForge.Api.Modules.Leads.Entities;
using SaaSForge.Api.Modules.Leads.Interfaces;
using SaaSForge.Api.Modules.Leads.Models;

namespace SaaSForge.Api.Modules.Leads.Services;

public class LeadService : ILeadService
{
    private readonly AppDbContext _context;
    private readonly ILeadActivityService _leadActivityService;

    public LeadService(
        AppDbContext context,
        ILeadActivityService leadActivityService)
    {
        _context = context;
        _leadActivityService = leadActivityService;
    }

    public async Task<Guid> CreateAsync(int businessId, CreateLeadRequest request)
    {
        ValidateCreateRequest(request);

        var utcNow = DateTime.UtcNow;

        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            FullName = request.FullName.Trim(),
            Email = NormalizeNullable(request.Email),
            Phone = NormalizeNullable(request.Phone),
            CompanyName = NormalizeNullable(request.CompanyName),
            Source = NormalizeSource(request.Source),
            Status = LeadStatuses.New,
            Priority = NormalizePriority(request.Priority),
            EstimatedValue = request.EstimatedValue,
            InquirySummary = NormalizeNullable(request.InquirySummary),
            LastIncomingMessagePreview = request.InitialMessage?.Trim(),
            LastIncomingAtUtc = string.IsNullOrWhiteSpace(request.InitialMessage) ? null : utcNow,
            LastContactAtUtc = string.IsNullOrWhiteSpace(request.InitialMessage) ? null : utcNow,
            NextFollowUpAtUtc = request.NextFollowUpAtUtc,
            IsArchived = false,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        _context.Leads.Add(lead);

        if (!string.IsNullOrWhiteSpace(request.InitialMessage))
        {
            var initialMessage = new LeadMessage
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                LeadId = lead.Id,
                Direction = LeadMessageDirections.Incoming,
                Channel = NormalizeChannel(request.Source),
                MessageType = "text",
                Content = request.InitialMessage.Trim(),
                IsAiGenerated = false,
                IsSent = false,
                CreatedAtUtc = utcNow
            };

            _context.LeadMessages.Add(initialMessage);
        }

        if (request.TagIds is { Count: > 0 })
        {
            var validTagIds = await _context.LeadTags
                .Where(x => x.BusinessId == businessId && request.TagIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

            foreach (var tagId in validTagIds.Distinct())
            {
                _context.LeadTagMaps.Add(new LeadTagMap
                {
                    LeadId = lead.Id,
                    TagId = tagId
                });
            }
        }

        await _context.SaveChangesAsync();

        await _leadActivityService.AddAsync(
            businessId,
            lead.Id,
            LeadActivityTypes.LeadCreated,
            "Lead created",
            $"Lead '{lead.FullName}' was created.");

        if (!string.IsNullOrWhiteSpace(request.InitialMessage))
        {
            await _leadActivityService.AddAsync(
                businessId,
                lead.Id,
                LeadActivityTypes.MessageReceived,
                "Initial message received",
                "Initial inquiry/message was saved.");
        }

        if (request.NextFollowUpAtUtc.HasValue)
        {
            await _leadActivityService.AddAsync(
                businessId,
                lead.Id,
                LeadActivityTypes.FollowUpScheduled,
                "Follow-up scheduled",
                $"Follow-up scheduled for {request.NextFollowUpAtUtc.Value:u}");
        }

        if (request.TagIds is { Count: > 0 })
        {
            await _leadActivityService.AddAsync(
                businessId,
                lead.Id,
                LeadActivityTypes.TagAdded,
                "Tags attached",
                "One or more tags were attached while creating the lead.");
        }

        return lead.Id;
    }

    public async Task<PagedResult<LeadListItemDto>> GetLeadsAsync(int businessId, LeadListQuery query)
    {
        query.Page = query.Page <= 0 ? 1 : query.Page;
        query.PageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, 100);

        var utcNow = DateTime.UtcNow;

        var dbQuery = _context.Leads
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId && !x.IsArchived);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();

            dbQuery = dbQuery.Where(x =>
                x.FullName.ToLower().Contains(search) ||
                (x.Email != null && x.Email.ToLower().Contains(search)) ||
                (x.Phone != null && x.Phone.ToLower().Contains(search)) ||
                (x.CompanyName != null && x.CompanyName.ToLower().Contains(search)) ||
                (x.InquirySummary != null && x.InquirySummary.ToLower().Contains(search)) ||
                (x.LastIncomingMessagePreview != null && x.LastIncomingMessagePreview.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            dbQuery = dbQuery.Where(x => x.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.Priority))
        {
            dbQuery = dbQuery.Where(x => x.Priority == query.Priority);
        }

        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            dbQuery = dbQuery.Where(x => x.Source == query.Source);
        }

        if (query.FollowUpDueOnly)
        {
            dbQuery = dbQuery.Where(x => x.NextFollowUpAtUtc != null && x.NextFollowUpAtUtc <= utcNow);
        }

        var totalCount = await dbQuery.CountAsync();

        var leads = await dbQuery
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new LeadListItemDto
            {
                Id = x.Id,
                FullName = x.FullName,
                Email = x.Email,
                Phone = x.Phone,
                Source = x.Source,
                Status = x.Status,
                Priority = x.Priority,
                EstimatedValue = x.EstimatedValue,
                LastIncomingMessagePreview = x.LastIncomingMessagePreview,
                LastIncomingAtUtc = x.LastIncomingAtUtc,
                LastReplyAtUtc = x.LastReplyAtUtc,
                NextFollowUpAtUtc = x.NextFollowUpAtUtc,
                CreatedAtUtc = x.CreatedAtUtc,
                Tags = x.LeadTags
                    .Select(t => t.Tag.Name)
                    .OrderBy(x => x)
                    .ToList()
            })
            .ToListAsync();

        return new PagedResult<LeadListItemDto>
        {
            Items = leads,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<LeadDetailsDto?> GetByIdAsync(int businessId, Guid leadId)
    {
        var lead = await _context.Leads
            .AsNoTracking()
            .Include(x => x.Messages)
            .Include(x => x.Notes)
            .Include(x => x.Activities)
            .Include(x => x.LeadTags)
                .ThenInclude(x => x.Tag)
            .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.Id == leadId && !x.IsArchived);

        if (lead is null)
            return null;

        return new LeadDetailsDto
        {
            Id = lead.Id,
            FullName = lead.FullName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            Source = lead.Source,
            Status = lead.Status,
            Priority = lead.Priority,
            EstimatedValue = lead.EstimatedValue,
            InquirySummary = lead.InquirySummary,
            LastIncomingMessagePreview = lead.LastIncomingMessagePreview,
            LastContactAtUtc = lead.LastContactAtUtc,
            LastReplyAtUtc = lead.LastReplyAtUtc,
            LastIncomingAtUtc = lead.LastIncomingAtUtc,
            NextFollowUpAtUtc = lead.NextFollowUpAtUtc,
            CreatedAtUtc = lead.CreatedAtUtc,
            UpdatedAtUtc = lead.UpdatedAtUtc,

            Messages = lead.Messages
                .OrderBy(x => x.CreatedAtUtc)
                .Select(x => new LeadMessageDto
                {
                    Id = x.Id,
                    Direction = x.Direction,
                    Channel = x.Channel,
                    MessageType = x.MessageType,
                    Content = x.Content,
                    IsAiGenerated = x.IsAiGenerated,
                    IsSent = x.IsSent,
                    AiTone = x.AiTone,
                    AiGoal = x.AiGoal,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToList(),

            Notes = lead.Notes
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new LeadNoteDto
                {
                    Id = x.Id,
                    Note = x.Note,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToList(),

            Activities = lead.Activities
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new LeadActivityDto
                {
                    Id = x.Id,
                    ActivityType = x.ActivityType,
                    Title = x.Title,
                    Description = x.Description,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToList(),

            Tags = lead.LeadTags
                .Select(x => new LeadTagDto
                {
                    Id = x.TagId,
                    Name = x.Tag.Name,
                    Color = x.Tag.Color
                })
                .OrderBy(x => x.Name)
                .ToList()
        };
    }

    public async Task UpdateAsync(int businessId, Guid leadId, UpdateLeadRequest request)
    {
        ValidateUpdateRequest(request);

        var lead = await GetLeadOrThrowAsync(businessId, leadId);

        lead.FullName = request.FullName.Trim();
        lead.Email = NormalizeNullable(request.Email);
        lead.Phone = NormalizeNullable(request.Phone);
        lead.CompanyName = NormalizeNullable(request.CompanyName);
        lead.Source = NormalizeSource(request.Source);
        lead.Priority = NormalizePriority(request.Priority);
        lead.EstimatedValue = request.EstimatedValue;
        lead.InquirySummary = NormalizeNullable(request.InquirySummary);
        lead.NextFollowUpAtUtc = request.NextFollowUpAtUtc;
        lead.IsArchived = request.IsArchived;
        lead.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(int businessId, Guid leadId, string status)
    {
        status = NormalizeStatus(status);

        var lead = await GetLeadOrThrowAsync(businessId, leadId);

        if (lead.Status == status)
            return;

        var oldStatus = lead.Status;

        lead.Status = status;
        lead.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _leadActivityService.AddAsync(
            businessId,
            lead.Id,
            LeadActivityTypes.StatusChanged,
            "Lead status updated",
            $"Status changed from '{oldStatus}' to '{status}'.");
    }

    public async Task ScheduleFollowUpAsync(int businessId, Guid leadId, DateTime? nextFollowUpAtUtc)
    {
        var lead = await GetLeadOrThrowAsync(businessId, leadId);

        lead.NextFollowUpAtUtc = nextFollowUpAtUtc;
        lead.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _leadActivityService.AddAsync(
            businessId,
            lead.Id,
            LeadActivityTypes.FollowUpScheduled,
            nextFollowUpAtUtc.HasValue ? "Follow-up scheduled" : "Follow-up cleared",
            nextFollowUpAtUtc.HasValue
                ? $"Follow-up scheduled for {nextFollowUpAtUtc.Value:u}"
                : "Next follow-up was removed.");
    }

    public async Task AddMessageAsync(int businessId, Guid leadId, AddLeadMessageRequest request)
    {
        ValidateMessageRequest(request);

        var lead = await GetLeadOrThrowAsync(businessId, leadId);
        var utcNow = DateTime.UtcNow;

        var message = new LeadMessage
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            LeadId = leadId,
            Direction = request.Direction.Trim(),
            Channel = NormalizeChannel(request.Channel),
            MessageType = request.IsAiGenerated ? "ai_generated_reply" : "text",
            Content = request.Content.Trim(),
            IsAiGenerated = request.IsAiGenerated,
            IsSent = request.IsSent,
            AiTone = NormalizeNullable(request.AiTone),
            AiGoal = NormalizeNullable(request.AiGoal),
            CreatedAtUtc = utcNow
        };

        _context.LeadMessages.Add(message);

        lead.LastContactAtUtc = utcNow;
        lead.UpdatedAtUtc = utcNow;

        if (request.Direction == LeadMessageDirections.Incoming)
        {
            lead.LastIncomingAtUtc = utcNow;
            lead.LastIncomingMessagePreview = request.Content.Trim().Length > 1000
                ? request.Content.Trim()[..1000]
                : request.Content.Trim();
        }
        else if (request.Direction == LeadMessageDirections.Outgoing)
        {
            lead.LastReplyAtUtc = utcNow;
        }

        await _context.SaveChangesAsync();

        await _leadActivityService.AddAsync(
            businessId,
            leadId,
            request.Direction == LeadMessageDirections.Incoming
                ? LeadActivityTypes.MessageReceived
                : LeadActivityTypes.ReplySaved,
            request.Direction == LeadMessageDirections.Incoming
                ? "Incoming message added"
                : "Outgoing reply added",
            $"Channel: {message.Channel}");
    }

    public async Task AddNoteAsync(int businessId, Guid leadId, AddLeadNoteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Note))
            throw new InvalidOperationException("Note is required.");

        var lead = await GetLeadOrThrowAsync(businessId, leadId);

        var note = new LeadNote
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            LeadId = leadId,
            Note = request.Note.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.LeadNotes.Add(note);

        lead.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _leadActivityService.AddAsync(
            businessId,
            leadId,
            LeadActivityTypes.NoteAdded,
            "Note added",
            "A note was added to this lead.");
    }

    public async Task AddTagAsync(int businessId, Guid leadId, Guid tagId)
    {
        var leadExists = await _context.Leads
            .AnyAsync(x => x.BusinessId == businessId && x.Id == leadId && !x.IsArchived);

        if (!leadExists)
            throw new InvalidOperationException("Lead not found.");

        var tagExists = await _context.LeadTags
            .AnyAsync(x => x.BusinessId == businessId && x.Id == tagId);

        if (!tagExists)
            throw new InvalidOperationException("Tag not found.");

        var alreadyMapped = await _context.LeadTagMaps
            .AnyAsync(x => x.LeadId == leadId && x.TagId == tagId);

        if (alreadyMapped)
            return;

        _context.LeadTagMaps.Add(new LeadTagMap
        {
            LeadId = leadId,
            TagId = tagId
        });

        await _context.SaveChangesAsync();

        var tagName = await _context.LeadTags
            .Where(x => x.Id == tagId)
            .Select(x => x.Name)
            .FirstAsync();

        await _leadActivityService.AddAsync(
            businessId,
            leadId,
            LeadActivityTypes.TagAdded,
            "Tag added",
            $"Tag '{tagName}' was added.");
    }

    public async Task RemoveTagAsync(int businessId, Guid leadId, Guid tagId)
    {
        var leadExists = await _context.Leads
            .AnyAsync(x => x.BusinessId == businessId && x.Id == leadId && !x.IsArchived);

        if (!leadExists)
            throw new InvalidOperationException("Lead not found.");

        var mapping = await _context.LeadTagMaps
            .FirstOrDefaultAsync(x => x.LeadId == leadId && x.TagId == tagId);

        if (mapping is null)
            return;

        var tagName = await _context.LeadTags
            .Where(x => x.BusinessId == businessId && x.Id == tagId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync();

        _context.LeadTagMaps.Remove(mapping);
        await _context.SaveChangesAsync();

        await _leadActivityService.AddAsync(
            businessId,
            leadId,
            LeadActivityTypes.TagRemoved,
            "Tag removed",
            tagName is null ? "A tag was removed." : $"Tag '{tagName}' was removed.");
    }

    public async Task ArchiveAsync(int businessId, Guid leadId)
    {
        var lead = await GetLeadOrThrowAsync(businessId, leadId);

        if (lead.IsArchived)
            return;

        lead.IsArchived = true;
        lead.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task<LeadDashboardSummaryDto> GetDashboardSummaryAsync(int businessId)
    {
        var utcNow = DateTime.UtcNow;

        var leadQuery = _context.Leads
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId && !x.IsArchived);

        return new LeadDashboardSummaryDto
        {
            TotalLeads = await leadQuery.CountAsync(),
            NewLeads = await leadQuery.CountAsync(x => x.Status == LeadStatuses.New),
            QualifiedLeads = await leadQuery.CountAsync(x => x.Status == LeadStatuses.Qualified),
            WonLeads = await leadQuery.CountAsync(x => x.Status == LeadStatuses.Won),
            LostLeads = await leadQuery.CountAsync(x => x.Status == LeadStatuses.Lost),
            FollowUpsDue = await leadQuery.CountAsync(x => x.NextFollowUpAtUtc != null && x.NextFollowUpAtUtc <= utcNow)
        };
    }

    private async Task<Lead> GetLeadOrThrowAsync(int businessId, Guid leadId)
    {
        var lead = await _context.Leads
            .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.Id == leadId && !x.IsArchived);

        if (lead is null)
            throw new InvalidOperationException("Lead not found.");

        return lead;
    }

    private static void ValidateCreateRequest(CreateLeadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new InvalidOperationException("FullName is required.");
    }

    private static void ValidateUpdateRequest(UpdateLeadRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new InvalidOperationException("FullName is required.");
    }

    private static void ValidateMessageRequest(AddLeadMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Direction))
            throw new InvalidOperationException("Direction is required.");

        if (!LeadMessageDirections.All.Contains(request.Direction))
            throw new InvalidOperationException("Invalid message direction.");

        if (string.IsNullOrWhiteSpace(request.Channel))
            throw new InvalidOperationException("Channel is required.");

        if (string.IsNullOrWhiteSpace(request.Content))
            throw new InvalidOperationException("Content is required.");
    }

    private static string NormalizeStatus(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Status is required.");

        var matched = LeadStatuses.All.FirstOrDefault(x =>
            string.Equals(x, value.Trim(), StringComparison.OrdinalIgnoreCase));

        if (matched is null)
            throw new InvalidOperationException("Invalid status.");

        return matched;
    }

    private static string NormalizePriority(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return LeadPriorities.Medium;

        var matched = LeadPriorities.All.FirstOrDefault(x =>
            string.Equals(x, value.Trim(), StringComparison.OrdinalIgnoreCase));

        if (matched is null)
            throw new InvalidOperationException("Invalid priority.");

        return matched;
    }

    private static string NormalizeSource(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "manual";

        return value.Trim();
    }

    private static string NormalizeChannel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return LeadChannels.Manual;

        var matched = LeadChannels.All.FirstOrDefault(x =>
            string.Equals(x, value.Trim(), StringComparison.OrdinalIgnoreCase));

        return matched ?? value.Trim();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public async Task MarkMessageSentAsync(int businessId, Guid leadId, Guid messageId, bool isSent)
    {
        var leadExists = await _context.Leads
            .AnyAsync(x => x.BusinessId == businessId && x.Id == leadId && !x.IsArchived);

        if (!leadExists)
            throw new InvalidOperationException("Lead not found.");

        var message = await _context.LeadMessages
            .FirstOrDefaultAsync(x =>
                x.BusinessId == businessId &&
                x.LeadId == leadId &&
                x.Id == messageId);

        if (message is null)
            throw new InvalidOperationException("Message not found.");

        if (message.Direction != LeadMessageDirections.Outgoing)
            throw new InvalidOperationException("Only outgoing messages can be marked as sent.");

        if (message.IsSent == isSent)
            return;

        message.IsSent = isSent;

        var lead = await _context.Leads
            .FirstAsync(x => x.BusinessId == businessId && x.Id == leadId);

        if (isSent)
        {
            lead.LastReplyAtUtc = DateTime.UtcNow;
            lead.LastContactAtUtc = DateTime.UtcNow;
        }

        lead.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _leadActivityService.AddAsync(
            businessId,
            leadId,
            LeadActivityTypes.ReplySaved,
            isSent ? "Message marked as sent" : "Message marked as draft",
            isSent
                ? "Outgoing message was marked as sent."
                : "Outgoing message was marked as not sent.");
    }

    public async Task UpdateMessageAsync(
    int businessId,
    Guid leadId,
    Guid messageId,
    UpdateLeadMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            throw new InvalidOperationException("Message content is required.");

        var lead = await _context.Leads
            .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.Id == leadId && !x.IsArchived);

        if (lead is null)
            throw new InvalidOperationException("Lead not found.");

        var message = await _context.LeadMessages
            .FirstOrDefaultAsync(x =>
                x.BusinessId == businessId &&
                x.LeadId == leadId &&
                x.Id == messageId);

        if (message is null)
            throw new InvalidOperationException("Message not found.");

        if (message.Direction != LeadMessageDirections.Outgoing)
            throw new InvalidOperationException("Only outgoing messages can be updated.");

        message.Content = request.Content.Trim();
        message.IsSent = request.IsSent;
        message.AiTone = string.IsNullOrWhiteSpace(request.AiTone) ? null : request.AiTone.Trim();
        message.AiGoal = string.IsNullOrWhiteSpace(request.AiGoal) ? null : request.AiGoal.Trim();

        var utcNow = DateTime.UtcNow;

        if (request.IsSent)
        {
            lead.LastReplyAtUtc = utcNow;
            lead.LastContactAtUtc = utcNow;
        }

        lead.UpdatedAtUtc = utcNow;

        await _context.SaveChangesAsync();

        await _leadActivityService.AddAsync(
            businessId,
            leadId,
            LeadActivityTypes.ReplySaved,
            request.IsSent ? "Outgoing message updated and marked sent" : "Outgoing draft updated",
            request.IsSent
                ? "Outgoing message was updated and marked as sent."
                : "Outgoing draft was updated.");
    }
}
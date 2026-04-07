namespace SaaSForge.Api.Modules.Leads.Entities
{
    public class LeadActivity
    {
        public Guid Id { get; set; }

        public int BusinessId { get; set; }

        public Guid LeadId { get; set; }
        public Lead Lead { get; set; } = default!;

        public string ActivityType { get; set; } = default!;
        public string Title { get; set; } = default!;
        public string? Description { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }
}

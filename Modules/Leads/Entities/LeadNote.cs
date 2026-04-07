namespace SaaSForge.Api.Modules.Leads.Entities
{
    public class LeadNote
    {
        public Guid Id { get; set; }

        public int BusinessId { get; set; }

        public Guid LeadId { get; set; }
        public Lead Lead { get; set; } = default!;

        public string Note { get; set; } = default!;

        public DateTime CreatedAtUtc { get; set; }
    }
}

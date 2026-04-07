namespace SaaSForge.Api.Modules.Leads.Entities
{
    public class LeadTagMap
    {
        public Guid LeadId { get; set; }
        public Lead Lead { get; set; } = default!;

        public Guid TagId { get; set; }
        public LeadTag Tag { get; set; } = default!;
    }
}

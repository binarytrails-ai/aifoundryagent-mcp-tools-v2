namespace ContosoBikestore.Agent.Host.Models
{
    public class ChatRequest
    {
        public string? AgentId { get; set; }
        public string? ThreadId { get; set; }
        public string? AgentName { get; set; }
        public string Message { get; set; }
    }
}

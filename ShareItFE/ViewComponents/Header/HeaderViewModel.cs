namespace ShareItFE.ViewComponents.Header
{
    public class HeaderViewModel
    {
        public bool IsUserLoggedIn { get; set; }
        public string? UserName { get; set; }
        public string? UserAvatarUrl { get; set; }
        public string? UserRole { get; set; }
        public int CartItemCount { get; set; }
    }
}

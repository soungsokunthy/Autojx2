using System;

namespace AutoJx2.Models
{
    /// <summary>
    /// Model containing game account information
    /// </summary>
    public class GameAccount
    {
        /// <summary>
        /// Unique account ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Username
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Password (encrypted)
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Server name
        /// </summary>
        public string Server { get; set; } = string.Empty;

        /// <summary>
        /// Creation timestamp
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Thời gian cập nhật cuối
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// Whether the account is hidden
        /// </summary>
        public bool IsHidden { get; set; } = false;

        /// <summary>
        /// Additional notes
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Process ID of the running game, if available
        /// </summary>
        public int? ProcessId { get; set; }

        /// <summary>
        /// Whether the game is currently running
        /// </summary>
        public bool IsRunning { get; set; } = false;

        /// <summary>
        /// Whether the item is selected in the ListView (not persisted to file)
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsSelected { get; set; } = false;
    }
}

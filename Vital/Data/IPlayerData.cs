namespace Vital.Data
{
    /// <summary>
    /// Interface for player data types that can be stored in VitalData.
    /// Implement this interface to create custom data that persists with the world
    /// and syncs automatically to clients.
    /// </summary>
    /// <example>
    /// <code>
    /// public class MyModData : IPlayerData
    /// {
    ///     public int Score { get; set; } = 0;
    ///     public List&lt;string&gt; Unlocks { get; set; } = new();
    ///
    ///     public void Initialize() { }
    ///     public string Serialize() => JsonUtility.ToJson(this);
    ///     public void Deserialize(string data) => JsonUtility.FromJsonOverwrite(data, this);
    ///     public bool Validate() => Score >= 0;
    /// }
    /// </code>
    /// </example>
    public interface IPlayerData
    {
        /// <summary>
        /// Serialize the data to a string for storage and network transfer.
        /// Use JSON or any other serialization format.
        /// </summary>
        /// <returns>Serialized data string.</returns>
        string Serialize();

        /// <summary>
        /// Deserialize from a string, populating this instance's values.
        /// </summary>
        /// <param name="data">Serialized data string.</param>
        void Deserialize(string data);

        /// <summary>
        /// Called when the data is first created for a new player.
        /// Use this to set default values.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Optional validation of data integrity.
        /// Called after deserialization to verify data is valid.
        /// </summary>
        /// <returns>True if data is valid, false otherwise.</returns>
        bool Validate();
    }
}

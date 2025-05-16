namespace Nonatomic.ServiceKit
{
	/// <summary>
	///     Defines how errors are handled during asynchronous injection if a custom handler is not provided.
	/// </summary>
	public enum ErrorHandlingMode
	{
		/// <summary>
		///     Rethrows the encountered exception. This is the default.
		/// </summary>
		Error,

		/// <summary>
		///     Logs the exception as a warning using Debug.LogWarning.
		/// </summary>
		Warning,

		/// <summary>
		///     Logs the exception as a message using Debug.Log.
		/// </summary>
		Log,

		/// <summary>
		///     Suppresses the exception silently.
		/// </summary>
		Silent
	}
}
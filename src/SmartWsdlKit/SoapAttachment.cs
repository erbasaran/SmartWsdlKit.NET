namespace SmartWsdlKit
{
	/// <summary>
	/// Represents an attachment for SOAP requests (MTOM or SOAP with Attachments).
	/// </summary>
	public class SoapAttachment
	{
		/// <summary>
		/// Gets the unique Content-ID of the attachment.
		/// </summary>
		public string ContentId { get; }

		/// <summary>
		/// Gets the MIME content type of the attachment (e.g. "application/octet-stream" or "image/png").
		/// </summary>
		public string ContentType { get; }

		/// <summary>
		/// Gets the raw binary data of the attachment.
		/// </summary>
		public byte[] Data { get; }

		/// <summary>
		/// Gets the optional filename of the attachment.
		/// </summary>
		public string? FileName { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="SoapAttachment"/> class.
		/// </summary>
		/// <param name="contentId">The unique attachment identifier.</param>
		/// <param name="data">The raw binary payload of the attachment.</param>
		/// <param name="contentType">The MIME type of the content.</param>
		/// <param name="fileName">Optional filename associated with this payload.</param>
		public SoapAttachment(string contentId, byte[] data, string contentType = "application/octet-stream", string? fileName = null)
		{
			ContentId = contentId ?? throw new ArgumentNullException(nameof(contentId));
			Data = data ?? throw new ArgumentNullException(nameof(data));
			ContentType = contentType;
			FileName = fileName;
		}
	}
}

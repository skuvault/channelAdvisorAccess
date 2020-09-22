using System;

namespace ChannelAdvisorAccess.Exceptions
{
	/// <summary>
	/// Exception thrown if ChannelAdvisor webservice returns an error.
	/// </summary>
	[ Serializable ]
	public class ChannelAdvisorException : Exception
	{
		private readonly int _messageCode;
		private readonly string _errorMessage;

		public ChannelAdvisorException( int code, string message ) : base( message )
		{
			this._messageCode = code;
			this._errorMessage = message;
		}

		public ChannelAdvisorException( string message ) : this( 0, message )
		{
		}

		public ChannelAdvisorException( string message, Exception exception )
			: base( message, exception )
		{
			this._errorMessage = message;
		}

		public ChannelAdvisorException( Exception exception )
			: base( exception.Message, exception )
		{
			this._errorMessage = exception.Message;
		}

		public int MessageCode
		{
			get { return this._messageCode; }
		}

		public string ErrorMessage
		{
			get { return this._errorMessage; }
		}

		///<summary>
		///Returns a <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
		///</summary>
		///
		///<returns>
		///A <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
		///</returns>
		///<filterpriority>2</filterpriority>
		public override string ToString()
		{
			var code = this.MessageCode;
			var errorMessage = this.ErrorMessage ?? string.Empty;
			return string.Format( "ErrorCode={0}, ErrorMessage={1}", code, errorMessage ) + "\n" + base.ToString();
		}
	}
}
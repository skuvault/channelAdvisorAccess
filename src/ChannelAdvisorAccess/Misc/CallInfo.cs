using System;
using System.Runtime.CompilerServices;

namespace ChannelAdvisorAccess.Misc
{
	public class CallInfoBasic
	{
		public Mark Mark { get; }
		public string MemberName { get; }
		public string ConnectionInfo { get; }
		public string AdditionalInfo { get; }
		public string MethodParameters { get; }

		public CallInfoBasic( string connectionInfo, string additionalInfo, string methodParameters = "", Mark mark = null, [ CallerMemberName ] string memberName = "" )
		{
			this.Mark = mark ?? Mark.Blank();
			this.MemberName = memberName;
			this.ConnectionInfo = connectionInfo;
			this.AdditionalInfo = additionalInfo;
			this.MethodParameters = methodParameters;
		}
	}

	public class CallInfo : CallInfoBasic
	{
		public string Payload { get; }
		public string MethodResult { get; }
		public string PayloadAndResponseLog
		{
			get
			{
				var payloadLog = string.IsNullOrWhiteSpace( Payload ) ? string.Empty : $", \nPayload:\"{Payload}\"";
				var resultLog = string.IsNullOrWhiteSpace( MethodResult ) ? string.Empty : $", \nResponse:{MethodResult}";
				return payloadLog + resultLog;
			}
		}
		public string NotesLog { get; }

		public CallInfo( string methodParameters = "", string payload = "", Mark mark = null, string methodResult = "", string additionalInfo = "", string notes = "", [ CallerMemberName ] string memberName = "", string connectionInfo = "" )
			: base( connectionInfo, additionalInfo, methodParameters, mark, memberName )
		{
			
			Payload = payload;
			MethodResult = methodResult;
			NotesLog = string.IsNullOrWhiteSpace( notes ) ? string.Empty : $", Notes:\"{notes}\"";
		}
	}

	public class RetryInfo : CallInfoBasic
	{
		public int RetryAttempt { get; }
		public double WaitDurationSecs { get; }

		public RetryInfo( int retryAttempt, double waitDurationSecs, [ CallerMemberName ] string memberName = "", string methodParameters = "" , string connectionInfo = "", string additionalInfo = "", Mark mark = null )
			: base( connectionInfo, additionalInfo, methodParameters, mark, memberName )
		{
			RetryAttempt = retryAttempt;
			WaitDurationSecs = waitDurationSecs;
		}
	}
}

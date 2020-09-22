using System;
using System.Runtime.CompilerServices;
using ChannelAdvisorAccess.Exceptions;
using ChannelAdvisorAccess.Misc;
using Newtonsoft.Json;

namespace ChannelAdvisorAccess.Services.Items
{
	public abstract class ServiceBaseAbstr
	{
		private Func< string > _additionalLogInfo;

		[ JsonIgnore ]
		public Func< string > AdditionalLogInfo
		{
			get { return this._additionalLogInfo ?? ( () => string.Empty ); }
			set { this._additionalLogInfo = value; }
		}

		protected Exception HandleExceptionAndLog( Mark mark, Exception exception, [ CallerMemberName ] string memberName = "", string methodParameters = "" )
		{
			var callInfo = new CallInfoBasic( connectionInfo: this.ToJson(), methodParameters: methodParameters, mark: mark, additionalInfo: this.AdditionalLogInfo(), memberName: memberName );
			var channelAdvisorException = new ChannelAdvisorException( exception );
			ChannelAdvisorLogger.LogTraceException( callInfo, channelAdvisorException );
			return channelAdvisorException;
		}
	}
}
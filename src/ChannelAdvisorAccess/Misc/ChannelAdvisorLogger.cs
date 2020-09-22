using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Netco.Logging;

namespace ChannelAdvisorAccess.Misc
{
	public class ChannelAdvisorLogger
	{
		private static readonly string _versionInfo;
		private const string ChannelType = "channelAdvisor";
		private const int MaxLogLineSize = 0xA00000; //10mb

		static ChannelAdvisorLogger()
		{
			Assembly assembly = Assembly.GetExecutingAssembly();
			_versionInfo = FileVersionInfo.GetVersionInfo( assembly.Location ).FileVersion;
		}

		public static ILogger Log()
		{
			return NetcoLogger.GetLogger( "ChannelAdvisorLogger" );
		}

		public static void LogTraceException( CallInfoBasic info, Exception exception, string message = "" )
		{			
			var logType = "An exception occurred";
			LogTraceException( logType, message, info, exception );
		}

		public static void LogTraceFailure( string message, CallInfoBasic info )
		{
			var logType = "A call failed";
			Log().Trace( "[{channel}] {type}:{Mark:\"{mark}\", MemberName:\"{callMemberName}\", ConnectionInfo:" + info.ConnectionInfo + ", " +
				"Message:\"" + message + "\", AdditionalInfo:{callAdditionalInfo}, [ver:{version}}", 
				ChannelType, logType, info.Mark, info.MemberName, info.AdditionalInfo, _versionInfo );
		}

		public static void LogTraceStarted( CallInfo info )
		{
			TraceLog( "Trace Start call", info );
		}

		public static void LogTraceEnd( CallInfo info )
		{
			TraceLog( "Trace End call", info );
		}

		public static void LogStarted( CallInfo info )
		{
			TraceLog( "Start call", info );
		}

		public static void LogEnd( CallInfo info )
		{
			TraceLog( "End call", info );
		}

		public static void LogTrace( CallInfo info )
		{
			TraceLog( "Trace info", info );
		}

		public static void LogTraceRetryStarted( CallInfo info )
		{
			TraceLog( "TraceRetryStarted info", info );
		}

		public static void LogTraceRetryException( RetryInfo info, Exception exception )
		{
			var logType = "Retry failed";
			var message = $"Call failed due to an exception, trying repeat call {info.RetryAttempt} time, waited {info.WaitDurationSecs} seconds. ";
			LogTraceException( logType, message, info, exception );
		}

		public static void LogTraceRetryScheduled( RetryInfo info, Exception exception )
		{
			var logType = "Retry scheduled after exception";
			var message = $"Retrying CA API get call for the {info.RetryAttempt} time, delay in secs: {info.WaitDurationSecs}. ";
			LogTraceException( logType, message, info, exception );
		}

		public static void LogTraceException( string logType, string message, CallInfoBasic info, Exception exception )
		{
			var messageLog = string.IsNullOrWhiteSpace( message ) ? string.Empty : $", Message:\"{message}\"";
			Log().Trace( exception, "[{channel}] {type}:{Mark:\"{mark}\", MemberName:\"{callMemberName}\", MethodParams:\"" + info.MethodParameters + "\", " + 
				"ConnectionInfo:" + info.ConnectionInfo + messageLog + ", AdditionalInfo:{callAdditionalInfo}, [ver:{version}}", 
				ChannelType, logType, info.Mark, info.MemberName, info.AdditionalInfo, _versionInfo );
		}

		public static void LogTraceRetryEnd( CallInfo info )
		{
			TraceLog( "TraceRetryEnd info", info );
		}

		private static void TraceLog( string logType, CallInfo info )
		{
			var payloadAndResponse = info.PayloadAndResponseLog;
			var formatWithPayloadAndResponse = "[{channel}] {type}:{Mark:\"{mark}\", MemberName:\"{callMemberName}\", MethodParams:\"" + info.MethodParameters + "\", " + 
				"ConnectionInfo:" + info.ConnectionInfo + info.NotesLog + payloadAndResponse + ", AdditionalInfo:{callAdditionalInfo}, [ver:{version}}";
			if( formatWithPayloadAndResponse.Length < MaxLogLineSize )
			{
				Log().Trace( formatWithPayloadAndResponse, ChannelType, logType, info.Mark, info.MemberName, info.AdditionalInfo, _versionInfo );
				return;
			}

			var pageNumber = 1;
			var pageId = Guid.NewGuid();
			foreach( var payloadAndResponsePage in SplitString( payloadAndResponse, MaxLogLineSize ) )
			{
				Log().Trace( "[{channel}] page:" + pageNumber++ + " pageId:" + pageId + " {type}:{Mark:\"{mark}\", MemberName:\"{callMemberName}\", MethodParams:\"" + info.MethodParameters + "\", " + 
					"ConnectionInfo:" + info.ConnectionInfo + info.NotesLog + payloadAndResponsePage + ", AdditionalInfo:{callAdditionalInfo}, [ver:{version}}", 
					ChannelType, logType, info.Mark, info.MemberName, info.AdditionalInfo, _versionInfo );
			}
		}

		private static IEnumerable< string > SplitString( string str, int chunkSize )
		{
			return Enumerable.Range( 0, str.Length / chunkSize )
				.Select( i => str.Substring( i * chunkSize, chunkSize ) );
		}

		public static string SanitizeToken( string token )
		{
			var length = token.Length;
			return token.Substring( 0, Math.Min( 4, length ) ) + "****" + token.Substring( length - Math.Min( 4, length ) ); 
		}
	}
}
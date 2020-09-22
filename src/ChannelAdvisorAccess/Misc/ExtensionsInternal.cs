using System;
using System.Runtime.CompilerServices;
using Netco.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ChannelAdvisorAccess.Misc
{
	public static class ExtensionsInternal
	{
		public static string ToJson( this object source )
		{
			try
			{
				if( source == null )
					return PredefinedValues.EmptyJsonObject;
				else
				{
					//var sourceConverted = source as IManualSerializable;
					//if (sourceConverted != null)
					//	return sourceConverted.SerializeToJson();
					//else
					//{
					var serialized = JsonConvert.SerializeObject( source, new IsoDateTimeConverter() );
					return serialized;
					//}
				}
			}
			catch( Exception )
			{
				return PredefinedValues.EmptyJsonObject;
			}
		}
	}
}
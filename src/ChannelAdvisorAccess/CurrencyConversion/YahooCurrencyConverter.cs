using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using CuttingEdge.Conditions;
using Zayko.Finance;

namespace ChannelAdvisorAccess.CurrencyConversion
{
	public sealed class YahooCurrencyConverter : ICurrencyConverter
	{
		private readonly string _baseCountryCode;
		private readonly CurrencyConverter _currencyConverter;
		private readonly Dictionary< string, decimal > _conversionRates;

		public YahooCurrencyConverter( string baseCountryCode )
		{
			Condition.Requires( baseCountryCode, "baseCountryCode" ).IsNotNullOrWhiteSpace();
			_baseCountryCode = baseCountryCode;
			_currencyConverter = new CurrencyConverter();
			_conversionRates = new Dictionary< string, decimal >();
		}

		public decimal ToBaseCurrency( string countryCode, decimal countryCurrency )
		{
			if( _baseCountryCode.Equals( countryCode, StringComparison.InvariantCultureIgnoreCase))
				return countryCurrency;

			return countryCurrency * GetRate( countryCode, _baseCountryCode );
		}

		public decimal ToCountryCurrency( string countryCode, decimal baseCurrency )
		{
			if( _baseCountryCode.Equals( countryCode, StringComparison.InvariantCultureIgnoreCase))
				return baseCurrency;

			return baseCurrency * GetRate( _baseCountryCode, countryCode );
		}

		private decimal GetRate( string sourceCode, string targetCode )
		{
			Condition.Requires( sourceCode, "sourceCode" ).IsNotNullOrWhiteSpace();
			Condition.Requires( targetCode, "targetCode" ).IsNotNullOrWhiteSpace();
			
			var rateKey = sourceCode + targetCode;
			decimal rate;
			if( _conversionRates.TryGetValue( rateKey, out rate ))
			{
				return rate;
			}
			else
			{
				var newRate = ( decimal )this._currencyConverter.GetCurrencyData( sourceCode, targetCode ).Rate;
				_conversionRates.Add( rateKey, newRate );
				return newRate;
			}
		}
	}
}
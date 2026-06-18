namespace Pek.Maths;

/// <summary>温度转换</summary>
public static class TemperatureConv
{
    /// <summary>摄氏度转换为华氏度</summary>
    /// <param name="value">摄氏度</param>
    public static Decimal DegreesCelsiusToFahrenheit(Decimal value) => (Decimal)1.8 * value + 32;

    /// <summary>摄氏度转换为开氏度(热力学温度)</summary>
    /// <param name="value">摄氏度</param>
    public static Decimal DegreesCelsiusToThermodynamicTemperature(Decimal value) => value + (Decimal)273.16;

    /// <summary>华氏度转换为摄氏度</summary>
    /// <param name="value">华氏度</param>
    public static Decimal FahrenheitToDegreesCelsius(Decimal value) => (value - 32) / (Decimal)1.8;

    /// <summary>华氏度转换为开氏度</summary>
    /// <param name="value">华氏度</param>
    public static Decimal FahrenheitToThermodynamicTemperature(Decimal value) => (value - 32) / (Decimal)1.8 + (Decimal)273.16;

    /// <summary>开氏度转换为摄氏度</summary>
    /// <param name="value">开氏度</param>
    public static Decimal ThermodynamicTemperatureToDegreesCelsius(Decimal value) => value - (Decimal)273.16;

    /// <summary>开氏度转换为华氏度</summary>
    /// <param name="value">开氏度</param>
    public static Decimal ThermodynamicTemperatureToFahrenheit(Decimal value) => (value - (Decimal)273.16) * (Decimal)1.8 + 32;
}

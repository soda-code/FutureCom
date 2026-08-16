namespace FutureCom.Models
{
    public enum ProtocolMode
    {
        ModbusRTU,          // 标准 Modbus-RTU (0x03)
        Yudian_AIBUS,       // 宇电 AI 系列温控/智能仪表
        DLT645_2007,        // 国家电网电能表通信规约
        CJT188_2018,        // 智能水表/燃气表/热量表规约
        ASCII_TextStream    // ASCII 换行符文本协议
    }

    public enum DataTypeOption
    {
        Int16_BigEndian,
        Int16_LittleEndian,
        UInt16_BigEndian,
        UInt16_LittleEndian,
        Float32_BigEndian,
        Float32_LittleEndian
    }

    public class AiProtocolRecommendation
    {
        public string ProtocolName { get; set; } = string.Empty;
        public ProtocolMode TargetMode { get; set; } = ProtocolMode.ModbusRTU;
        public int RecommendedBaudRate { get; set; } = 9600;
        public string SendHex { get; set; } = string.Empty;
        public int ExpectedLength { get; set; } = 7;
        public int DataOffset { get; set; } = 3;
        public DataTypeOption DataType { get; set; } = DataTypeOption.Int16_BigEndian;
        public double ScaleFactor { get; set; } = 0.1;
        public string Explanation { get; set; } = string.Empty;
    }
}
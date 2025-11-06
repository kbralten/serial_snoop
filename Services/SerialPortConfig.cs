using RJCP.IO.Ports;

namespace SerialSnoop.Wpf.Services;

public class SerialPortConfig
{
    public string PortName { get; set; } = "";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public Parity Parity { get; set; } = Parity.None;
    public StopBits StopBits { get; set; } = StopBits.One;
    public Handshake Handshake { get; set; } = Handshake.None;
    public bool DtrEnable { get; set; } = false;
    public bool RtsEnable { get; set; } = false;

    public void ApplyTo(SerialPortStream port)
    {
        port.PortName = PortName;
        port.BaudRate = BaudRate;
        port.DataBits = DataBits;
        port.Parity = Parity;
        port.StopBits = StopBits;
        port.Handshake = Handshake;
        port.DtrEnable = DtrEnable;
        port.RtsEnable = RtsEnable;
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DnsManager.Core.Models;

/// <summary>Пресет DNS-профиля: именованный набор серверов. Name с INotifyPropertyChanged для автосохранения.</summary>
public sealed class DnsPreset : INotifyPropertyChanged
{
	private string _name = "";

	public Guid Id { get; init; } = Guid.NewGuid();

	public string Name
	{
		get => _name;
		set
		{
			if (_name != value)
			{
				_name = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
			}
		}
	}

	public ObservableCollection<DnsServerSetting> Servers { get; init; } = [];

	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>Пресет по умолчанию из ТЗ.</summary>
	public static DnsPreset Default() => new()
	{
		Name = "111.88.96.50/51 (по умолчанию)",
		Servers =
		[
			DnsServerSetting.PrimaryProfile("111.88.96.50"),
			DnsServerSetting.SecondaryProfile("111.88.96.51")
		]
	};
}

using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DnsManager.Core.Models;

/// <summary>Пресет DNS-профиля: именованный набор серверов. Name с INotifyPropertyChanged для автосохранения.</summary>
public sealed class DnsPreset : INotifyPropertyChanged
{
	private string _name = "";
	private bool _isDefault;

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

	/// <summary>Признак профиля по умолчанию; в хранилище гарантируется ровно один такой профиль.</summary>
	public bool IsDefault
	{
		get => _isDefault;
		set
		{
			if (_isDefault != value)
			{
				_isDefault = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDefault)));
			}
		}
	}

	public ObservableCollection<DnsServerSetting> Servers { get; init; } = [];

	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>Пресет по умолчанию из ТЗ.</summary>
	public static DnsPreset Default() => new()
	{
		Name = "111.88.96.50/51 (по умолчанию)",
		IsDefault = true,
		Servers =
		[
			DnsServerSetting.PrimaryProfile("111.88.96.50"),
			DnsServerSetting.SecondaryProfile("111.88.96.51")
		]
	};
}

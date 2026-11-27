using DarkSunRE.Commands;
using DarkSunRE.Models;
using DarkSunRE.Services;
using DSunTools.Common.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;

namespace DarkSunRE.ViewModels
{
    public sealed class InterfaceFieldViewModel
    {
        public InterfaceFieldViewModel(string name, string originalValue, string value, bool numeric)
        {
            Name = name;
            OriginalValue = originalValue;
            Value = value;
            IsNumeric = numeric;
        }

        public string Name { get; }
        public string OriginalValue { get; }
        public string Value { get; set; }
        public bool IsNumeric { get; }
    }

    public sealed class InterfaceItemViewModel
    {
        public InterfaceItemViewModel(string tag, uint id, short originalX, short originalY)
        {
            Tag = tag;
            Id = id;
            OriginalX = originalX;
            OriginalY = originalY;
            X = originalX;
            Y = originalY;
        }

        public string Tag { get; }
        public uint Id { get; }
        public short OriginalX { get; }
        public short OriginalY { get; }
        public short X { get; set; }
        public short Y { get; set; }
    }

    public sealed class InterfaceResourceJson
    {
        public string Tag { get; set; }
        public int Number { get; set; }
        public List<InterfaceFieldJson> Fields { get; set; } = new();
        public List<InterfaceItemJson> Items { get; set; } = new();
    }

    public sealed class InterfaceFieldJson
    {
        public string Name { get; set; }
        public string OriginalValue { get; set; }
        public string Value { get; set; }
    }

    public sealed class InterfaceItemJson
    {
        public string Tag { get; set; }
        public uint Id { get; set; }
        public short OriginalX { get; set; }
        public short OriginalY { get; set; }
        public short X { get; set; }
        public short Y { get; set; }
    }

    /// <summary>
    /// Editable WIND/BUTN interface resources. Original values come from the
    /// untouched game container; current values may come from a generated
    /// file. Save writes only resource.ModifiedData.
    /// </summary>
    public sealed class InterfaceResourceViewModel : DocumentViewModel
    {
        private readonly IStatusService _statusService;
        private Project _project;
        private ResourceFile _container;
        private ResourceInfo _resourceInfo;
        private byte[] _originalBytes;
        private WindResource _currentWind;
        private ButnResource _currentButn;

        public InterfaceResourceViewModel(IStatusService statusService)
        {
            _statusService = statusService;
            CanClose = true;
            Items = new ObservableCollection<InterfaceItemViewModel>();
            Fields = new ObservableCollection<InterfaceFieldViewModel>();
            SaveCommand = new RelayCommand(Save);
            ExportJsonCommand = new RelayCommand(ExportJson);
            ImportJsonCommand = new RelayCommand(ImportJson);
        }

        public bool IsWindow { get; private set; }
        public bool IsButton => !IsWindow;
        public string Kind { get; private set; }
        public ObservableCollection<InterfaceItemViewModel> Items { get; }
        public ObservableCollection<InterfaceFieldViewModel> Fields { get; }
        public ICommand SaveCommand { get; }
        public ICommand ExportJsonCommand { get; }
        public ICommand ImportJsonCommand { get; }

        public void Initialize(
            Project project,
            ResourceFile container,
            ResourceInfo resourceInfo,
            byte[] originalBytes,
            byte[] currentBytes)
        {
            _project = project;
            _container = container;
            _resourceInfo = resourceInfo;
            _originalBytes = originalBytes == null ? Array.Empty<byte>() : (byte[])originalBytes.Clone();
            string tag = ResourceCatalog.NormalizeTag(resourceInfo.Tag).TrimEnd();
            IsWindow = tag == "WIND";
            Kind = IsWindow ? "Window layout" : "Button";
            Title = $"{container.Name}:{resourceInfo.Tag.Trim()}-{resourceInfo.Number}";
            File = container.FullPath;

            Items.Clear();
            Fields.Clear();

            if (IsWindow)
            {
                WindResource original = WindResource.Parse(originalBytes);
                _currentWind = WindResource.Parse(currentBytes);
                foreach (WindItem current in _currentWind.Items)
                {
                    WindItem originalItem = original.Items.FirstOrDefault(item =>
                        string.Equals(item.Tag, current.Tag, StringComparison.Ordinal) && item.Id == current.Id);
                    Items.Add(new InterfaceItemViewModel(
                        current.Tag,
                        current.Id,
                        originalItem?.X ?? current.X,
                        originalItem?.Y ?? current.Y));
                }
            }
            else
            {
                ButnResource original = ButnResource.Parse(originalBytes);
                _currentButn = ButnResource.Parse(currentBytes);
                AddField("Width", original.Width, _currentButn.Width, true);
                AddField("Height", original.Height, _currentButn.Height, true);
                AddField("IconX", original.IconX, _currentButn.IconX, true);
                AddField("IconY", original.IconY, _currentButn.IconY, true);
                AddField("TextX", original.TextX, _currentButn.TextX, true);
                AddField("TextY", original.TextY, _currentButn.TextY, true);
                AddField("IconId", original.IconId, _currentButn.IconId, true);
                AddField("Hotkey", original.Hotkey, _currentButn.Hotkey, true);
                AddField("Text", original.Text, _currentButn.Text, false);
            }

            IsModified = false;
        }

        public override void Save()
        {
            try
            {
                if (IsWindow)
                {
                    foreach (WindItem item in _currentWind.Items)
                    {
                        InterfaceItemViewModel edited = Items.FirstOrDefault(candidate =>
                            string.Equals(candidate.Tag, item.Tag, StringComparison.Ordinal) && candidate.Id == item.Id);
                        if (edited == null)
                            continue;
                        item.X = edited.X;
                        item.Y = edited.Y;
                    }

                    _resourceInfo.ModifiedData = _currentWind.Build();
                }
                else
                {
                    _currentButn.Width = ReadInt("Width");
                    _currentButn.Height = ReadInt("Height");
                    _currentButn.IconX = ReadInt("IconX");
                    _currentButn.IconY = ReadInt("IconY");
                    _currentButn.TextX = ReadInt("TextX");
                    _currentButn.TextY = ReadInt("TextY");
                    _currentButn.IconId = ReadUInt("IconId");
                    _currentButn.Hotkey = (byte)ReadInt("Hotkey");
                    _currentButn.Text = Field("Text").Value ?? string.Empty;
                    _resourceInfo.ModifiedData = _currentButn.Build();
                }

                _resourceInfo.IsModified = true;
                IsModified = false;
                _statusService?.ShowSuccess($"Updated {_resourceInfo.Tag.Trim()}-{_resourceInfo.Number}. Use Generate new file to write it.");
            }
            catch (Exception ex)
            {
                _statusService?.ShowError($"Failed to save interface resource: {ex.Message}", ex);
            }
        }

        private void ExportJson()
        {
            string path = BuildJsonPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                _statusService?.ShowError("JSON folder is not configured.", null);
                return;
            }

            try
            {
                // Capture the editor's current values first, then write one
                // ui.json with every WIND/BUTN record of this GFF container.
                Save();
                InterfaceJsonStore.Export(_project, _container);
                _statusService?.ShowSuccess($"UI JSON saved: {path}");
            }
            catch (Exception ex)
            {
                _statusService?.ShowError($"Failed to save UI JSON: {ex.Message}", ex);
            }
        }

        private void ImportJson()
        {
            string path = BuildJsonPath();
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                _statusService?.ShowError($"UI JSON not found: {path}", null);
                return;
            }

            try
            {
                int applied = InterfaceJsonStore.Apply(_project, _container, path);
                if (applied == 0)
                    throw new InvalidDataException("ui.json contains no matching WIND/BUTN records.");

                byte[] current = _resourceInfo.ModifiedData ?? _originalBytes;
                Initialize(_project, _container, _resourceInfo, _originalBytes, current);
                _statusService?.ShowSuccess($"UI JSON loaded: {path} ({applied} record(s))");
            }
            catch (Exception ex)
            {
                _statusService?.ShowError($"Failed to load UI JSON: {ex.Message}", ex);
            }
        }

        public string BuildJsonPath()
        {
            return InterfaceJsonStore.GetPath(_project, _container);
        }

        private void AddField(string name, object original, object current, bool numeric)
        {
            Fields.Add(new InterfaceFieldViewModel(
                name,
                Convert.ToString(original, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(current, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                numeric));
        }

        private InterfaceFieldViewModel Field(string name)
        {
            return Fields.First(field => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private int ReadInt(string name)
        {
            InterfaceFieldViewModel field = Field(name);
            if (!int.TryParse(field.Value, out int value))
                throw new InvalidDataException($"{name} must be an integer.");
            return value;
        }

        private uint ReadUInt(string name)
        {
            InterfaceFieldViewModel field = Field(name);
            if (!uint.TryParse(field.Value, out uint value))
                throw new InvalidDataException($"{name} must be an unsigned integer.");
            return value;
        }

        public override void Reload()
        {
            // The parent document re-opens the resource.
        }
    }
}

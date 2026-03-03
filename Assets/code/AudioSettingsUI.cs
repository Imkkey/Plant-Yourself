using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Voice.Unity;

public class AudioSettingsUI : MonoBehaviour
{
    [Header("Настройки Микрофона")]
    public TMP_Dropdown micDropdown;
    
    [Header("Настройки Громкости")]
    public Slider masterVolumeSlider;
    public Slider voiceVolumeSlider;

    private List<string> _micDevices = new List<string>();

    private void Start()
    {
        InitializeMicrophoneList();
        InitializeVolumeSettings();
    }

    private void InitializeMicrophoneList()
    {
        if (micDropdown == null) return;

        micDropdown.ClearOptions();
        _micDevices.Clear();

        // Добавляем дефолтную опцию
        _micDevices.Add("По умолчанию (System Default)");

        // Получаем все доступные микрофоны от Unity
        foreach (string device in Microphone.devices)
        {
            _micDevices.Add(device);
        }

        micDropdown.AddOptions(_micDevices);

        // Загружаем сохраненный микрофон
        string savedMic = PlayerPrefs.GetString("SelectedMicrophone", "");
        int savedIndex = 0;

        if (!string.IsNullOrEmpty(savedMic))
        {
            // Ищем индекс сохраненного микрофона
            savedIndex = _micDevices.IndexOf(savedMic);
            if (savedIndex == -1) savedIndex = 0; // Если старый микрофон отключили
        }

        micDropdown.value = savedIndex;
        micDropdown.RefreshShownValue();

        // Привязываем событие при смене
        micDropdown.onValueChanged.AddListener(OnMicChanged);
        
        // Применяем текущий сохранённый (или дефолтный) микрофон при старте
        ApplyMicrophone(savedIndex);
    }

    private void OnMicChanged(int index)
    {
        ApplyMicrophone(index);
    }

    private void ApplyMicrophone(int index)
    {
        string selectedMic = index == 0 ? "" : _micDevices[index];
        PlayerPrefs.SetString("SelectedMicrophone", selectedMic);
        PlayerPrefs.Save();

        // Ищем рекордер в сцене (если мы уже в игре и рекордер заспавнился)
        Recorder localRecorder = FindAnyObjectByType<Recorder>();
        if (localRecorder != null && localRecorder.isActiveAndEnabled)
        {
            // Меняем устройство на лету
            if (index == 0)
            {
                // Сброс на "По умолчанию"
                localRecorder.MicrophoneDevice = new Photon.Voice.DeviceInfo("[Default]");
            }
            else
            {
                // Выбран конкретный микрофон
                localRecorder.MicrophoneDevice = new Photon.Voice.DeviceInfo(selectedMic);
            }
        }
    }

    private void InitializeVolumeSettings()
    {
        // Мастер громкость (общая)
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
            
            // Сразу применяем к Unity AudioListener
            AudioListener.volume = masterVolumeSlider.value;
            
            masterVolumeSlider.onValueChanged.AddListener(val => {
                AudioListener.volume = val;
                PlayerPrefs.SetFloat("MasterVolume", val);
                PlayerPrefs.Save();
            });
        }

        // Громкость других игроков (Photon Voice)
        // Меняем громкость на AudioListener нельзя (это общая), нужно менять громкость на Speaker'ах или в глобальном сэмплере.
        // Пока можно просто сохранять значение, а на спавне игроков - устанавливать громкость их AudioSource.
        if (voiceVolumeSlider != null)
        {
            voiceVolumeSlider.minValue = 0f;
            voiceVolumeSlider.maxValue = 1f;
            voiceVolumeSlider.value = PlayerPrefs.GetFloat("VoiceVolume", 1f);
            
            voiceVolumeSlider.onValueChanged.AddListener(val => {
                PlayerPrefs.SetFloat("VoiceVolume", val);
                PlayerPrefs.Save();
                
                // Обновляем аудио-сурсы на ходу для уже заспавненных игроков
                Speaker[] speakers = FindObjectsByType<Speaker>(FindObjectsSortMode.None);
                foreach (var speaker in speakers)
                {
                    AudioSource src = speaker.GetComponent<AudioSource>();
                    if (src != null)
                    {
                        src.volume = val;
                    }
                }
            });
        }
    }
}

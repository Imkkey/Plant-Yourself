using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class NewsData
{
    [Tooltip("Заголовок новости (будет на кнопке и внутри самой новости)")]
    public string title;
    
    [Tooltip("Содержание патчноута/новости")]
    [TextArea(5, 10)] // Позволяет писать много текста прямо в инспекторе
    public string content;
}

public class NewsUI : MonoBehaviour
{
    [Header("Панели")]
    [Tooltip("Панель со списком патчноутов (с заголовками)")]
    public GameObject listPanel;
    [Tooltip("Панель прочтения самой новости (там где весь текст)")]
    public GameObject articlePanel;

    [Header("Список новостей (Scroll View)")]
    [Tooltip("Объект Content внутри Viewport списка новостей")]
    public Transform listContent;
    [Tooltip("Префаб кнопки с заголовком новости (Button + TextMeshProUGUI)")]
    public GameObject newsItemPrefab; 

    [Header("Окно прочтения (Article Panel)")]
    [Tooltip("Текст, куда вставится заголовок читаемой новости")]
    public TMP_Text articleTitleText;
    [Tooltip("Текст, куда вставится текст новости")]
    public TMP_Text articleContentText;
    [Tooltip("Scroll View в котором находится текст новости")]
    public GameObject articleScrollView;
    [Tooltip("Кнопка 'Назад' (возврат к списку заголовков)")]
    public Button btnBack;

    [Header("Контент новостей")]
    [Tooltip("Здесь добавляй сами патчноуты и новости")]
    public List<NewsData> newsList = new List<NewsData>();

    private void Start()
    {
        // Привязываем кнопку "Назад"
        if (btnBack != null) 
        {
            btnBack.onClick.AddListener(ShowList);
        }

        // Генерируем список 
        PopulateList();
        
        // По умолчанию показываем список заголовков, при открытии вкладки новостей
        ShowList();
    }

    private void OnEnable()
    {
        // Когда игрок открывает панель новостей через MainMenuUI - всегда показываем список заголовков
        ShowList();
    }

    private void PopulateList()
    {
        if (listContent == null || newsItemPrefab == null) return;

        // Очищаем существующие плашки перед обновлением
        foreach (Transform child in listContent)
        {
            Destroy(child.gameObject);
        }

        // Извлекаем новости. Сделаем так, чтобы новые (последние) показывались сверху, если хочется?
        // Пока просто по порядку.
        for (int i = 0; i < newsList.Count; i++)
        {
            NewsData news = newsList[i];
            
            // Создаем кнопку в Content
            GameObject itemObj = Instantiate(newsItemPrefab, listContent);
            
            // Ищем текст заголовка на префабе
            TMP_Text titleText = itemObj.GetComponentInChildren<TMP_Text>();
            if (titleText != null)
            {
                titleText.text = news.title;
            }

            // Назначаем событие клика
            Button btn = itemObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => OpenArticle(news));
            }
        }
    }

    private void OpenArticle(NewsData news)
    {
        // Заполняем интерфейс прочтения
        if (articleTitleText != null) articleTitleText.text = news.title;
        if (articleContentText != null) 
        {
            articleContentText.text = news.content;
        }

        // Явно включаем Scroll View
        if (articleScrollView != null)
        {
            articleScrollView.SetActive(true);
        }
        
        // Отключаем список, включаем чтение
        if (listPanel != null) listPanel.SetActive(false);
        if (articlePanel != null) articlePanel.SetActive(true);
    }

    public void ShowList()
    {
        // Отключаем чтение, включаем список
        if (listPanel != null) listPanel.SetActive(true);
        if (articlePanel != null) articlePanel.SetActive(false);

        // На всякий случай очищаем текст (на случай, если он не является дочерним объектом ArticlePanel)
        if (articleTitleText != null) articleTitleText.text = "";
        if (articleContentText != null) articleContentText.text = "";

        // Явно выключаем Scroll View
        if (articleScrollView != null)
        {
            articleScrollView.SetActive(false);
        }
    }
}

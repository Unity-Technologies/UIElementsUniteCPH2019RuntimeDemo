using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class UIScreenManager : VisualElement
{
    public new class UxmlFactory : UxmlFactory<UIScreenManager, UxmlTraits> { }

    public new class UxmlTraits : VisualElement.UxmlTraits
    {
        UxmlBoolAttributeDescription m_ShowMenuScreen = new UxmlBoolAttributeDescription { name = "show-menu-screen", defaultValue = true };
        UxmlBoolAttributeDescription m_ShowGameScreen = new UxmlBoolAttributeDescription { name = "show-game-screen", defaultValue = false };
        UxmlBoolAttributeDescription m_ShowEndScreen = new UxmlBoolAttributeDescription { name = "show-end-screen", defaultValue = false };

        public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
        {
            base.Init(ve, bag, cc);

            var v = ((UIScreenManager)ve);

            v.showMenuScreen = m_ShowMenuScreen.GetValueFromBag(bag, cc);
            v.showGameScreen = m_ShowGameScreen.GetValueFromBag(bag, cc);
            v.showEndScreen = m_ShowEndScreen.GetValueFromBag(bag, cc);
        }
    }

    bool m_ShowMenuScreen;
    bool m_ShowGameScreen;
    bool m_ShowEndScreen;

    VisualElement m_MenuScreen;
    VisualElement m_GameScreen;
    VisualElement m_EndScreen;

    public bool showMenuScreen {
        get => m_ShowMenuScreen;
        set
        {
            if (value == m_ShowMenuScreen)
                return;

            m_ShowMenuScreen = value;
            if (m_MenuScreen == null)
                return;

            m_MenuScreen.style.display = m_ShowMenuScreen ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
    public bool showGameScreen
    {
        get => m_ShowGameScreen;
        set
        {
            if (value == m_ShowGameScreen)
                return;

            m_ShowGameScreen = value;
            if (m_GameScreen == null)
                return;

            m_GameScreen.style.display = m_ShowGameScreen ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
    public bool showEndScreen
    {
        get => m_ShowEndScreen;
        set
        {
            if (value == m_ShowEndScreen)
                return;

            m_ShowEndScreen = value;
            if (m_EndScreen == null)
                return;

            m_EndScreen.style.display = m_ShowEndScreen ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    public VisualElement menuScreen => getMenuScreen();
    public VisualElement gameScreen => getGameScreen();
    public VisualElement endScreen => getEndScreen();

    public UIScreenManager()
    {
        RegisterCallback<GeometryChangedEvent>(FirstInit);
    }

    VisualElement getMenuScreen()
    {
        if (m_MenuScreen != null)
            return m_MenuScreen;

        m_MenuScreen = this.Q("menu-screen");
        if (m_MenuScreen == null)
            return m_MenuScreen;

        m_MenuScreen.style.display = m_ShowMenuScreen ? DisplayStyle.Flex : DisplayStyle.None;

        return m_MenuScreen;
    }
    VisualElement getGameScreen()
    {
        if (m_GameScreen != null)
            return m_GameScreen;

        m_GameScreen = this.Q("game-screen");
        if (m_GameScreen == null)
            return m_GameScreen;

        m_GameScreen.style.display = m_ShowGameScreen ? DisplayStyle.Flex : DisplayStyle.None;

        return m_GameScreen;
    }
    VisualElement getEndScreen()
    {
        if (m_EndScreen != null)
            return m_EndScreen;

        m_EndScreen = this.Q("end-screen");
        if (m_EndScreen == null)
            return m_EndScreen;

        m_EndScreen.style.display = m_ShowEndScreen ? DisplayStyle.Flex : DisplayStyle.None;

        return m_EndScreen;
    }

    void FirstInit(GeometryChangedEvent evt)
    {
        getMenuScreen();
        getGameScreen();
        getEndScreen();
    }
}

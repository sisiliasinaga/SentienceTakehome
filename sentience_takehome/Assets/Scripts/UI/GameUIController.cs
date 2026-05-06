using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using JetBrains.Annotations;
using UnityEngine.UI;

public class GameUIController : MonoBehaviour
{
    [Header("Start Panel")]
    public GameObject startPanel;
    public Button aiButton;
    public Button multiplayerButton;

    [Header("Main Panel")]
    public GameObject mainPanel;
    public TMP_Text currentShipText;
    public TMP_Text orientationText;
    public TMP_Text turnText;
    public TMP_Text feedbackText;
    public TMP_Text gameOverText;

    [Header("Multiplayer Panel")]
    public GameObject multiplayerPanel;
    public Button createGameButton;
    public Button joinGameButton;

    [Header("Create Game Panel")]
    public GameObject createGamePanel;
    public TMP_Text joinCodeText;
    public TMP_Text gameStatusText;

    [Header("Join Game Panel")]
    public GameObject joinGamePanel;
    public TMP_InputField joinCodeInputField;
    public TMP_Text joinGameStatusText;

    [Header("Game Over Panel")]
    public GameObject gameOverPanel;
    public TMP_Text gameOverWinnerText;
    public TMP_Text gameOverTurnsText;
    public TMP_Text gameOverShipsSunkText;

    public void SetCurrentShip(ShipType shipType)
    {
        currentShipText.text = $"Current ship: {shipType}";
    }

    public void SetOrientation(Orientation orientation)
    {
        orientationText.text = $"Orientation: {orientation}";
    }

    public void SetTurn(bool isPlayerTurn)
    {
        turnText.text = isPlayerTurn ? "Your Turn" : "Enemy Turn";
    }

    public void SetFeedback(string message)
    {
        feedbackText.text = message;
    }

    public void SetGameOver(string message)
    {
        gameOverText.text = message;
    }

    public void ClearGameOver()
    {
        gameOverText.text = "";
    }

    public void ShowMainPanel()
    {
        startPanel.SetActive(false);
        mainPanel.SetActive(true);
        multiplayerPanel.SetActive(false);
        createGamePanel.SetActive(false);
        joinGamePanel.SetActive(false);
        gameOverPanel.SetActive(false);
    }

    public void HideMainPanel()
    {
        mainPanel.SetActive(false);
    }

    public void ShowGameOver(string winnerMessage, string turnsMessage, string shipsSunkMessage)
    {
        mainPanel.SetActive(false);
        gameOverPanel.SetActive(true);
        gameOverWinnerText.text = winnerMessage;
        gameOverTurnsText.text = turnsMessage;
        gameOverShipsSunkText.text = shipsSunkMessage;
    }

    public void HideGameOver()
    {
        gameOverPanel.SetActive(false);
    }

    public void ShowStartPanel()
    {
        startPanel.SetActive(true);
        mainPanel.SetActive(false);
        multiplayerPanel.SetActive(false);
        createGamePanel.SetActive(false);
        joinGamePanel.SetActive(false);
        gameOverPanel.SetActive(false);
    }

    public void HideStartPanel()
    {
        startPanel.SetActive(false);
    }

    public void ShowMultiplayerPanel()
    {
        startPanel.SetActive(false);
        mainPanel.SetActive(false);
        multiplayerPanel.SetActive(true);
        createGamePanel.SetActive(false);
        joinGamePanel.SetActive(false);
        gameOverPanel.SetActive(false);
    }

    public void HideMultiplayerPanel()
    {
        multiplayerPanel.SetActive(false);
    }

    public void ShowCreateGamePanel()
    {
        startPanel.SetActive(false);
        multiplayerPanel.SetActive(false);  
        mainPanel.SetActive(false);
        createGamePanel.SetActive(true);
        joinGamePanel.SetActive(false);
        gameOverPanel.SetActive(false);
    }

    public void HideCreateGamePanel()
    {
        createGamePanel.SetActive(false);
    }

    public void ShowJoinGamePanel()
    {
        startPanel.SetActive(false);
        multiplayerPanel.SetActive(false);
        mainPanel.SetActive(false);
        createGamePanel.SetActive(false);
        joinGamePanel.SetActive(true);
        gameOverPanel.SetActive(false);
    }
    
    public void HideJoinGamePanel()
    {
        joinGamePanel.SetActive(false);
    }
}

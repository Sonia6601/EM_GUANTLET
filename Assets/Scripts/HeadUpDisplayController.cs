using UnityEngine;
using UnityEngine.UI;

public class HeadUpDisplayController : MonoBehaviour
{
    private enum HudSlot
    {
        Yellow,
        Red,
        Purple,
        Green
    }

    [System.Serializable]
    private class HudBlock
    {
        public HudSlot slot;
        public GameObject root;

        [Header("Hearts")]
        public Image imageHeartTens;
        public Image imageHeartUnits;

        [Header("Keys")]
        public Image imageKeyUnits;

        [Header("Diamonds")]
        public Image imageDiamondsHundreds;
        public Image imageDiamondTens;
        public Image imageDiamondUnits;
    }

    [Header("Bloques de HUD por personaje")]
    [SerializeField] private HudBlock[] hudBlocks;

    [Header("Single Player")]
    [SerializeField] private bool hideNonSelectedBlocks = true;

    [Header("Sprites de cifras")]
    [SerializeField] private Sprite spriteZero;
    [SerializeField] private Sprite spriteOne;
    [SerializeField] private Sprite spriteTwo;
    [SerializeField] private Sprite spriteThree;
    [SerializeField] private Sprite spriteFour;
    [SerializeField] private Sprite spriteFive;
    [SerializeField] private Sprite spriteSix;
    [SerializeField] private Sprite spriteSeven;
    [SerializeField] private Sprite spriteEight;
    [SerializeField] private Sprite spriteNine;

    private HudBlock activeBlock;

    /// <summary>
    /// Resuelve el bloque activo según el personaje seleccionado y actualiza su visibilidad inicial.
    /// </summary>
    private void Awake()
    {
        resolveActiveBlockFromSelectedCharacter();
        refreshBlockVisibility();

    }

    /// <summary>
    /// Suscribe los eventos de actualización del HUD al habilitar el componente.
    /// </summary>
    private void OnEnable()
    {
        GameEvents.OnHealthChanged += UpdateHearts;
        GameEvents.OnKeysChanged += UpdateKeys;
        GameEvents.OnDiamondsChanged += UpdateDiamonds;
    }

    /// <summary>
    /// Desuscribe los eventos de actualización del HUD al deshabilitar el componente.
    /// </summary>
    private void OnDisable()
    {
        GameEvents.OnHealthChanged -= UpdateHearts;
        GameEvents.OnKeysChanged -= UpdateKeys;
        GameEvents.OnDiamondsChanged -= UpdateDiamonds;
    }

    /// <summary>
    /// Actualiza los dígitos de vida del bloque de HUD activo.
    /// </summary>
    public void UpdateHearts(int hearts)
    {
        if (activeBlock == null) return;

        if (hearts < 0) hearts = 0;

        int tens = hearts / 10;
        int units = hearts % 10;

        Sprite tensSprite = getSpriteForDigit(tens);
        Sprite unitsSprite = getSpriteForDigit(units);

        if (activeBlock.imageHeartTens != null && activeBlock.imageHeartTens.sprite != tensSprite)
            activeBlock.imageHeartTens.sprite = tensSprite;

        if (activeBlock.imageHeartUnits != null && activeBlock.imageHeartUnits.sprite != unitsSprite)
            activeBlock.imageHeartUnits.sprite = unitsSprite;
    }

    /// <summary>
    /// Actualiza el dígito de llaves del bloque de HUD activo.
    /// </summary>
    public void UpdateKeys(int keys)
    {
        if (activeBlock == null) return;

        //keys = GameManager.Instance != null ? GameManager.Instance.GetKeys() : 0;
        int units = keys % 10;
        Sprite unitsSprite = getSpriteForDigit(units);

        if (activeBlock.imageKeyUnits != null && activeBlock.imageKeyUnits.sprite != unitsSprite)
            activeBlock.imageKeyUnits.sprite = unitsSprite;

    }

    /// <summary>
    /// Actualiza los dígitos de diamantes del bloque de HUD activo.
    /// </summary>
    public void UpdateDiamonds(int diamonds)
    {
        if (activeBlock == null) return;

        //diamonds = GameManager.Instance != null ? GameManager.Instance.GetDiamonds() : 0;
        int hundreds = diamonds / 100;
        int tens = (diamonds % 100) / 10;
        int units = diamonds % 10;

        Sprite hundredsSprite = getSpriteForDigit(hundreds);
        Sprite tensSprite = getSpriteForDigit(tens);
        Sprite unitsSprite = getSpriteForDigit(units);

        if (activeBlock.imageDiamondsHundreds != null && activeBlock.imageDiamondsHundreds.sprite != hundredsSprite)
            activeBlock.imageDiamondsHundreds.sprite = hundredsSprite;

        if (activeBlock.imageDiamondTens != null && activeBlock.imageDiamondTens.sprite != tensSprite)
            activeBlock.imageDiamondTens.sprite = tensSprite;

        if (activeBlock.imageDiamondUnits != null && activeBlock.imageDiamondUnits.sprite != unitsSprite)
            activeBlock.imageDiamondUnits.sprite = unitsSprite;
    }

    /// <summary>
    /// Determina el bloque HUD activo en función del nombre del personaje seleccionado.
    /// </summary>
    private void resolveActiveBlockFromSelectedCharacter()
    {
        activeBlock = findBlockBySlot(HudSlot.Green);

        string characterName = GameManager.Instance?.SelectedCharacterStats?.characterName;
        if (string.IsNullOrEmpty(characterName)) return;

        string characterNameLowerCase = characterName.ToLowerInvariant();

        if (characterNameLowerCase.Contains("yellow")) activeBlock = findBlockBySlot(HudSlot.Yellow);
        else if (characterNameLowerCase.Contains("red")) activeBlock = findBlockBySlot(HudSlot.Red);
        else if (characterNameLowerCase.Contains("purple")) activeBlock = findBlockBySlot(HudSlot.Purple);
        else if (characterNameLowerCase.Contains("green")) activeBlock = findBlockBySlot(HudSlot.Green);
    }

    /// <summary>
    /// Busca y devuelve el bloque de HUD asociado al slot indicado.
    /// </summary>
    private HudBlock findBlockBySlot(HudSlot slot)
    {
        if (hudBlocks == null) return null;

        for (int i = 0; i < hudBlocks.Length; i++)
        {
            if (hudBlocks[i] != null && hudBlocks[i].slot == slot)
                return hudBlocks[i];
        }

        return null;
    }

    /// <summary>
    /// Activa u oculta bloques de HUD según la configuración y el bloque seleccionado.
    /// </summary>
    private void refreshBlockVisibility()
    {
        if (hudBlocks == null) return;

        for (int i = 0; i < hudBlocks.Length; i++)
        {
            HudBlock block = hudBlocks[i];
            if (block == null || block.root == null) continue;

            bool visible = !hideNonSelectedBlocks || block == activeBlock;
            block.root.SetActive(visible);
        }
    }

    private void refreshKeys(int totalKeys)
    {
        if (activeBlock == null) return;

        int units = totalKeys % 10;

        if (activeBlock.imageKeyUnits != null)
            activeBlock.imageKeyUnits.sprite = getSpriteForDigit(units);
    }

    private void refreshDiamonds(int totalDiamonds)
    {
        if (activeBlock == null) return;

        // Ya no necesitamos GameManager.Instance.GetDiamonds();
        int hundreds = (totalDiamonds / 100) % 10;
        int tens = (totalDiamonds / 10) % 10;
        int units = totalDiamonds % 10;

        if (activeBlock.imageDiamondsHundreds != null)
            activeBlock.imageDiamondsHundreds.sprite = getSpriteForDigit(hundreds);

        if (activeBlock.imageDiamondTens != null)
            activeBlock.imageDiamondTens.sprite = getSpriteForDigit(tens);

        if (activeBlock.imageDiamondUnits != null)
            activeBlock.imageDiamondUnits.sprite = getSpriteForDigit(units);
    }


    /// <summary>
    /// Devuelve el sprite correspondiente al dígito solicitado.
    /// </summary>
    private Sprite getSpriteForDigit(int digit)
    {
        return digit switch
        {
            0 => spriteZero,
            1 => spriteOne,
            2 => spriteTwo,
            3 => spriteThree,
            4 => spriteFour,
            5 => spriteFive,
            6 => spriteSix,
            7 => spriteSeven,
            8 => spriteEight,
            9 => spriteNine,
            _ => spriteZero
        };
    }
}

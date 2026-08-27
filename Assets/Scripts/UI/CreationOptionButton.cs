using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DemocracyWay.UI
{
    using DemocracyWay.Core;
    using DemocracyWay.Framework;

    /// <summary>
    /// One row in the character-creation option list on the right-hand side.
    ///
    /// It reports two different things, which is why it doesn't reuse
    /// <see cref="MenuButton"/> directly: hovering *previews* an option on the
    /// left panel without committing to it, while clicking *selects* it. A
    /// selected row keeps its highlight after the pointer leaves.
    /// </summary>
    [AddComponentMenu("DemocracyWay/Creation Option Button")]
    public class CreationOptionButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;
        [SerializeField] private Image background;
        [SerializeField] private Image selectionBar;

        [Header("Colours")]
        [SerializeField] private Color idleBackground     = new Color(1f, 1f, 1f, 0.04f);
        [SerializeField] private Color hoverBackground    = new Color(1f, 1f, 1f, 0.12f);
        [SerializeField] private Color selectedBackground = new Color(0.85f, 0.75f, 0.40f, 0.22f);
        [SerializeField] private Color idleText           = new Color(0.88f, 0.86f, 0.80f);
        [SerializeField] private Color selectedText       = new Color(1f, 0.94f, 0.72f);

        private CreationOption option;
        private Action<CreationOption> onHover;
        private Action<CreationOption> onClick;
        private bool isSelected;

        public CreationOption Option => option;

        public void Bind(CreationOption opt, Action<CreationOption> hoverCallback, Action<CreationOption> clickCallback)
        {
            option  = opt;
            onHover = hoverCallback;
            onClick = clickCallback;

            if (titleLabel != null) titleLabel.text = opt != null ? opt.title : string.Empty;
            if (subtitleLabel != null)
            {
                string sub = opt != null ? opt.subtitle : string.Empty;
                subtitleLabel.text = sub;
                subtitleLabel.gameObject.SetActive(!string.IsNullOrEmpty(sub));
            }

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (background != null)
                background.color = selected ? selectedBackground : idleBackground;
            if (titleLabel != null)
                titleLabel.color = selected ? selectedText : idleText;
            if (selectionBar != null)
                selectionBar.gameObject.SetActive(selected);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (background != null && !isSelected) background.color = hoverBackground;
            AudioManager.Instance?.PlayButtonHover();
            onHover?.Invoke(option);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (background != null && !isSelected) background.color = idleBackground;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            AudioManager.Instance?.PlayButtonClick();
            onClick?.Invoke(option);
        }
    }
}

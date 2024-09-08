using UnityEngine;
using UnityEngine.UI;

using KSP.UI.TooltipTypes;

namespace Stapler
{
    internal class StaplerButton : MonoBehaviour
    {
        public Stapler stapler;

        public GameObject reparentButton;
        public Toggle toggle;

        public static StaplerButton Create(Stapler stapler)
        {
            var button = EditorLogic.fetch.toolsUI.gameObject.AddComponent<StaplerButton>();
            button.stapler = stapler;

            return button;
        }

        protected void Start()
        {
            CreateButton();
        }

        private void CreateButton()
        {
            EditorToolsUI toolsUI = EditorLogic.fetch.toolsUI;

            // Clone the root button.
            reparentButton = Instantiate(toolsUI.rootButton.gameObject);
            reparentButton.transform.SetParent(toolsUI.rootButton.transform.parent, false);
            reparentButton.name = "reparentButton";

            // Adjust the position.
            reparentButton.transform.localPosition = FindPosition();

            // Change tooltip.
            reparentButton.GetComponent<TooltipController_Text>().SetText("Tool: Re-Parent");

            // Get toggle and original sprite.
            toggle = reparentButton.GetComponent<Toggle>();
            var oldSprite = toggle.image.sprite;

            // Replace off sprite.
            Texture2D offIconTexture = GameDatabase.Instance.GetTexture("Stapler/Icons/reparent_off", false);
            reparentButton.GetChild("Background").GetComponent<Image>().sprite = Sprite.Create(offIconTexture, oldSprite.rect, oldSprite.pivot);

            // Replace on sprite.
            Texture2D onIconTexture = GameDatabase.Instance.GetTexture("Stapler/Icons/reparent_on", false);
            reparentButton.GetChild("Checkmark").GetComponent<Image>().sprite = Sprite.Create(onIconTexture, oldSprite.rect, oldSprite.pivot);

            // Add a listener for button selection.
            toggle.onValueChanged.AddListener(OnReparentButtonInput);
        }

        private void OnReparentButtonInput(bool isOn)
        {
            if (isOn && reparentButton.GetComponent<Toggle>().interactable)
                stapler.SetMode(false);
        }

        public static Vector3 FindPosition()
        {
            EditorToolsUI toolsUI = EditorLogic.fetch.toolsUI;
            Vector3 pos = toolsUI.rootButton.transform.localPosition;

            // What's the horizontal offset for one of these buttons?
            float offset = toolsUI.rootButton.transform.localPosition.x
                - toolsUI.rotateButton.transform.localPosition.x;

            // Highly silly tweakscale side-step. Should find a better way and PR to TweakScale.
            pos.x = toolsUI.rootButton.transform.localPosition.x + offset * (Stapler.hasTweakScale ? 2 : 1);

            return pos;
        }
    }
}

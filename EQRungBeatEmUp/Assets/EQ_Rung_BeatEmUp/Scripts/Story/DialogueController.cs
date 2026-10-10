using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace BeatEmUp.Story
{
    public sealed class DialogueController : MonoBehaviour
    {
        public DialogueDatabase database;
        public TMP_FontAsset font;
        public event Action<string> Callback;
        public event Action AdvanceRequested, SkipRequested, RetryRequested;
        public event Action<int> ChoiceRequested;
        public bool Thai { get; private set; } = true;
        public bool Paused { get; set; }
        public event Action LanguageChanged;
        public bool IsTalking => current != null;
        public bool Typing => current != null && visible < body.textInfo.characterCount;
        public string ConversationId { get; private set; }
        public int LineIndex { get; private set; } = -1;
        public int Revision { get; private set; }
        public float Fade { get; private set; }
        public Color FadeColor { get; private set; }
        Canvas canvas;
        GameObject dialogueBox, practiceButtons;
        TextMeshProUGUI speaker, body, hint, instruction, objective, boss;
        Image portrait, shade;
        Button skip, advance;
        DialogueLine current;
        float visible, elapsed;
        bool choiceMade;
        AudioSource blip;
        readonly System.Collections.Generic.List<GameObject> choiceButtons = new System.Collections.Generic.List<GameObject>();
        void Awake()
        {
            Thai = PlayerPrefs.GetInt("EQRung.Story.Thai",1)==1;
            var root=new GameObject("Story UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); root.transform.SetParent(transform,false);
            canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=500;
            var scaler=root.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1280,720);
            shade=Panel(root.transform,"Story fade",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,Color.clear); shade.raycastTarget=false;
            objective=Label(root.transform,"Chapter",new Vector2(.05f,.83f),new Vector2(.95f,.95f),28); objective.alignment=TextAlignmentOptions.Center;
            instruction=Label(root.transform,"Tutorial",new Vector2(.1f,.68f),new Vector2(.9f,.82f),24); instruction.alignment=TextAlignmentOptions.Center;
            boss=Label(root.transform,"Boss health",new Vector2(.25f,.92f),new Vector2(.75f,.98f),22); boss.alignment=TextAlignmentOptions.Center;
            dialogueBox=Panel(root.transform,"Dialogue",new Vector2(.03f,.03f),new Vector2(.97f,.30f),Vector2.zero,Vector2.zero,new Color(.035f,.045f,.08f,.97f)).gameObject;
            portrait=Panel(dialogueBox.transform,"Portrait",new Vector2(.015f,.1f),new Vector2(.155f,.9f),Vector2.zero,Vector2.zero,Color.white); portrait.preserveAspect=true;
            speaker=Label(dialogueBox.transform,"Speaker",new Vector2(.18f,.71f),new Vector2(.96f,.94f),27); speaker.color=new Color(1,.78f,.38f);
            body=Label(dialogueBox.transform,"Words",new Vector2(.18f,.18f),new Vector2(.97f,.72f),28);
            hint=Label(dialogueBox.transform,"Advance hint",new Vector2(.18f,.02f),new Vector2(.97f,.18f),16); hint.alignment=TextAlignmentOptions.Right;
            advance=Button(root.transform,"Advance",new Vector2(.81f,.32f),new Vector2(.97f,.38f),()=>AdvanceRequested?.Invoke());
            skip=Button(root.transform,"Skip scene",new Vector2(.81f,.40f),new Vector2(.97f,.46f),()=>SkipRequested?.Invoke());
            Button(root.transform,"ไทย / EN",new Vector2(.03f,.94f),new Vector2(.14f,.99f),()=>SetLanguage(!Thai));
            practiceButtons=Button(root.transform,"Retry checkpoint",new Vector2(.03f,.32f),new Vector2(.22f,.38f),()=>RetryRequested?.Invoke()).gameObject;
            blip=gameObject.AddComponent<AudioSource>(); blip.volume=.18f;
            if(!FindFirstObjectByType<EventSystem>()) { var es=new GameObject("Story EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)); es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions(); }
            Hide(); SetCinematic(false,false);
        }
        TextMeshProUGUI Label(Transform parent,string name,Vector2 min,Vector2 max,int size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>(); rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;
            var text=go.GetComponent<TextMeshProUGUI>();text.font=font;text.fontSize=size;text.color=Color.white;text.raycastTarget=false;text.richText=false;return text;
        }
        static Image Panel(Transform parent,string name,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=offsetMin;rt.offsetMax=offsetMax;
            var image=go.GetComponent<Image>();image.color=color;return image;
        }
        Button Button(Transform parent,string caption,Vector2 min,Vector2 max,Action action)
        {
            var image=Panel(parent,caption,min,max,Vector2.zero,Vector2.zero,new Color(.1f,.14f,.22f,.95f));var button=image.gameObject.AddComponent<Button>();
            var text=Label(image.transform,caption,Vector2.zero,Vector2.one,18);text.text=caption;text.alignment=TextAlignmentOptions.Center;
            button.onClick.AddListener(()=>action());return button;
        }
        public void SetLanguage(bool thai)
        {
            Thai=thai;PlayerPrefs.SetInt("EQRung.Story.Thai",thai ? 1 : 0);
            if(current!=null) { body.text=current.Text(Thai);body.ForceMeshUpdate(); visible=0; }
            LanguageChanged?.Invoke();
        }
        public void Show(string id,int line)
        {
            var conversation=database.Find(id);
            if(conversation==null || line<0 || line>=conversation.lines.Count) { Hide();return; }
            if(ConversationId==id && LineIndex==line && current!=null) return;
            ConversationId=id;LineIndex=line;Revision++; current=conversation.lines[line];visible=elapsed=0;choiceMade=false;
            dialogueBox.SetActive(true);advance.gameObject.SetActive(true);speaker.text=current.speaker;body.text=current.Text(Thai);body.maxVisibleCharacters=0;body.ForceMeshUpdate();
            portrait.sprite=current.portrait;portrait.enabled=current.portrait;ClearChoices();
        }
        public bool Advance()
        {
            if(current==null) return true;
            if(Typing) { visible=body.textInfo.characterCount;body.maxVisibleCharacters=(int)visible;return false; }
            if(current.choices.Length>0 && !choiceMade) return false;
            Callback?.Invoke(current.callback);return true;
        }
        public bool Choose(int index)
        {
            if(current==null || Typing || Paused || index<0 || index>=current.choices.Length || choiceMade)return false;
            choiceMade=true;Callback?.Invoke(current.choices[index].flag);return true;
        }
        public void Hide() { current=null;ConversationId=null;LineIndex=-1; if(dialogueBox)dialogueBox.SetActive(false);if(advance)advance.gameObject.SetActive(false);ClearChoices(); }
        void ClearChoices() { foreach(var go in choiceButtons) Destroy(go);choiceButtons.Clear(); }
        public void SetInstruction(string value) { instruction.text=value??""; }
        public void SetObjective(string value) { objective.text=value??""; }
        public void SetBoss(float hp,float max) { boss.text=max>0 ? "THROWER — "+Mathf.CeilToInt(hp)+" / "+Mathf.CeilToInt(max) : ""; }
        public void SetFade(float value,Color color) { Fade=Mathf.Clamp01(value);FadeColor=color;shade.color=new Color(color.r,color.g,color.b,Fade); }
        public void SetCinematic(bool value,bool active) { skip.gameObject.SetActive(value);practiceButtons.SetActive(active && !value); }
        public void SetVisible(bool value) { canvas.enabled=value; }
        void Update()
        {
            if(current==null || Paused) return;
            int before=(int)visible;elapsed+=Time.unscaledDeltaTime;
            visible+=Time.unscaledDeltaTime*Mathf.Max(1,current.charactersPerSecond);body.maxVisibleCharacters=(int)visible;
            if((int)visible>before && current.voiceBlip && !blip.isPlaying) blip.PlayOneShot(current.voiceBlip);
            hint.text=Typing ? (Thai ? "ยืนยัน: แสดงข้อความทั้งหมด" : "Confirm: show text") : (Thai ? "ยืนยัน: ถัดไป" : "Confirm: next");
            if(!Typing && current.choices.Length>0 && choiceButtons.Count==0)
                for(int i=0;i<current.choices.Length;i++) { int index=i;var choice=current.choices[i];var b=Button(dialogueBox.transform,Thai ? choice.thai : choice.english,new Vector2(.18f+i*.25f,.02f),new Vector2(.4f+i*.25f,.18f),()=>ChoiceRequested?.Invoke(index));choiceButtons.Add(b.gameObject); }
            if(!Typing && current.autoAdvanceSeconds>0 && elapsed>body.textInfo.characterCount/Mathf.Max(1,current.charactersPerSecond)+current.autoAdvanceSeconds) { elapsed=0;AdvanceRequested?.Invoke(); }
        }
    }
}

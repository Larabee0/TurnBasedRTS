using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Unity.Collections;

public class CSVDialogue : MonoBehaviour
{
	[SerializeField] private List<NPCID> IDs = new List<NPCID>();
	private Dictionary<NPCID, List<DialogueInfo>> AllDialogue = new Dictionary<NPCID, List<DialogueInfo>>();
	string[][] splitLoad;
	string path;
	// Start is called before the first frame update
	private void Start()
    {
		LoadFile("NPCDialogue");
		StartCoroutine(NewDialogue());
	}

	private IEnumerator NewDialogue()
	{
		List<DialogueInfo> dialogues = AllDialogue[IDs[UnityEngine.Random.Range(0, IDs.Count)]];
		DialogueInfo dialogue = dialogues[UnityEngine.Random.Range(0, dialogues.Count)];
		dialogue.views++;
		Debug.Log(dialogue.views + ". " + dialogue.text);


		yield return new WaitForSeconds(2.5f);
		StartCoroutine(NewDialogue());
	}

	public enum Mood
    {
		Happy,
		Sad,
		Angry
    }

	private void LoadFile(string fileName)
	{
        path = Path.Combine(Application.persistentDataPath, fileName + ".csv");
		string[] fileLoad = File.ReadAllLines(path);

		splitLoad = new string[fileLoad.Length][];

		for (int i = 0; i < fileLoad.Length; i++)
		{
			string current = fileLoad[i];
			splitLoad[i] = current.Split(new[] { ',' }, StringSplitOptions.None);
		}

		Dictionary<NPCID, List<DialogueInfo>> MoodDictionary = new Dictionary<NPCID, List<DialogueInfo>>();
		//List<Mood> MoodsUsed = new List<Mood>();
		for (int i = 0; i < splitLoad.Length; i++)
		{
			string ID = splitLoad[i][0];
			string mood = splitLoad[i][1];
			string dialogue = splitLoad[i][2];
            int views;
            if (splitLoad[i].Length < 4)
			{
				views = 0;
			}
			else
			{
				views = int.Parse(splitLoad[i][3]);
			}
			
			Mood currentMood;
			
			if (mood == "Happy")
			{
				currentMood = Mood.Happy;
			}
			else if (mood == "Sad")
			{
				currentMood = Mood.Sad;
			}
			else // else must be angry
			{
				currentMood = Mood.Angry;
			}

			NPCID id = new NPCID(int.Parse(ID), currentMood);

			DialogueInfo newDialogue = new DialogueInfo(id, currentMood, dialogue, views, i);

			if (MoodDictionary.ContainsKey(id))
			{
				MoodDictionary[id].Add(newDialogue);
			}
			else
			{
				MoodDictionary.Add(id, new List<DialogueInfo> { newDialogue });
				//MoodsUsed.Add(currentMood);
				IDs.Add(id);
			}
		}

		AllDialogue = MoodDictionary;

	}

    private void OnDestroy()
    {
        for (int i = 0; i < IDs.Count; i++)
        {
			List<DialogueInfo> currentDialogue = AllDialogue[IDs[i]];
            for (int k = 0; k < currentDialogue.Count; k++)
            {
				DialogueInfo current = currentDialogue[k];
				int LineOfOrigin = current.LineOfOrigin;

				if(splitLoad[LineOfOrigin].Length < 4)
                {
					string[] line = new string[splitLoad[LineOfOrigin].Length + 1];
                    for (int j = 0; j < splitLoad[LineOfOrigin].Length; j++)
                    {
						line[j] = splitLoad[LineOfOrigin][j];

					}
					line[3] = Convert.ToString(current.views);
					splitLoad[LineOfOrigin] = line;

				}
                else
                {
					splitLoad[LineOfOrigin][3] = Convert.ToString(current.views);
				}
            }
		}

		// now need to deserialize splitLoad into the csv file;
		int length = splitLoad.GetLength(0);
		StringBuilder stringbuilder = new StringBuilder();
        for (int i = 0; i < length; i++)
        {
			stringbuilder.AppendLine(string.Join(",", splitLoad[i]));
        }

		File.WriteAllText(path, stringbuilder.ToString());
	}

    public class DialogueInfo
	{
		public NPCID ID;
		public int IDnpc;
		public int LineOfOrigin;
		public Mood mood;
		public string text;
		public int views;

		public DialogueInfo(int id, string text, Mood mood, int views, int LineOfOrigin)
        {
			IDnpc = id;
			this.mood = mood;
			this.text = text;
			this.views = views;
			this.LineOfOrigin = LineOfOrigin;
		}

		public DialogueInfo(NPCID id, Mood mood, string text, int views, int LineOfOrigin)
		{
			ID = id;
			this.mood = mood;
			this.text = text;
			this.views = views;
			this.LineOfOrigin = LineOfOrigin;
		}

	}


	public struct NPCID
    {
		public int NPC;
		public Mood Mood;

		public NPCID(int npc, Mood mood)
		{
			NPC = npc;
			Mood = mood;
		}
    }
}

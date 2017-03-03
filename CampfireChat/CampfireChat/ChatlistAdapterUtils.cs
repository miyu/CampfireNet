using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Support.V7.Widget;

namespace CampfireChat
{
	class ChatlistAdapter : RecyclerView.Adapter
	{
		public ChatEntry[] Entries;

		public ChatlistAdapter(ChatEntry[] entries)
		{
			Entries = entries;
		}

		public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
		{
			View itemView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.ChatHistory, parent, false);

			ChatlistViewHolder vh = new ChatlistViewHolder(itemView);
			return vh;
		}

		public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
		{
			ChatlistViewHolder vh = holder as ChatlistViewHolder;

			ChatEntry entry = Entries[position];

			if (entry.Names.Length == 1)
			{
				vh.Names.Text = entry.Names[0];
			}
			else if (entry.Names.Length == 2)
			{
				vh.Names.Text = entry.Names[0] + ", " + entry.Names[1];
			}
			else
			{
				vh.Names.Text = entry.Names[0] + ", and " + (entry.Names.Length - 1) + " others";
			}


			vh.Preview.Text = entry.PreviewLine;
		}

		public override int ItemCount
		{
			get { return Entries.Length; }
		}
	}

	public class ChatlistViewHolder : RecyclerView.ViewHolder
	{
		public TextView Names { get; private set; }
		public TextView Preview { get; private set; }

		public ChatlistViewHolder(View itemView) : base(itemView)
		{
			Preview = itemView.FindViewById<TextView>(Resource.Id.Preview);
			Names = itemView.FindViewById<TextView>(Resource.Id.Names);
		}
	}

	public class ChatEntry
	{
		public string[] Names { get; private set; }
		public string PreviewLine { get; private set; }

		public ChatEntry(string[] names, string previewLine)
		{
			Names = names;
			PreviewLine = previewLine;
		}
	}
}
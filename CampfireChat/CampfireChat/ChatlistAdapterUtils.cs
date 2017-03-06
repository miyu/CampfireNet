using System;
using Android.Views;
using Android.Widget;
using Android.Support.V7.Widget;
using System.Collections.Generic;

namespace CampfireChat
{
	class ChatlistAdapter : RecyclerView.Adapter
	{
		public List<ChatEntry> Entries { get; set; }
		public event EventHandler<Title> ItemClick;

		public ChatlistAdapter(List<ChatEntry> entries)
		{
			Entries = entries;
		}

		public void AddEntry(int position, ChatEntry entry)
		{
			Entries.Insert(position, entry);
			NotifyItemInserted(position);
		}

		public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
		{
			View itemView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.ChatEntry, parent, false);

			ChatlistViewHolder vh = new ChatlistViewHolder(itemView, OnClick);
			return vh;
		}

		public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
		{
			ChatlistViewHolder vh = holder as ChatlistViewHolder;
			ChatEntry entry = Entries[position];

			vh.FriendlyName.Text = entry.FriendlyName;
			vh.Preview.Text = entry.PreviewLine;
		}

		private void OnClick(int position)
		{
			if (ItemClick != null)
			{
				string title = Entries[position].FriendlyName;
				ItemClick(this, new Title(title, position));
			}
		}

		public override int ItemCount
		{
			get { return Entries.Count; }
		}
	}

	public class ChatlistViewHolder : RecyclerView.ViewHolder
	{
		public TextView FriendlyName { get; private set; }
		public TextView Preview { get; private set; }

		public ChatlistViewHolder(View itemView, Action<int> listener) : base(itemView)
		{
			Preview = itemView.FindViewById<TextView>(Resource.Id.Preview);
			FriendlyName = itemView.FindViewById<TextView>(Resource.Id.FriendlyName);

			itemView.Clickable = true;
			itemView.Click += (sender, e) => listener(AdapterPosition);
			//itemView.Touch += (object sender, View.TouchEventArgs e) => listener(base.AdapterPosition, e.Event.Action));
		}
	}

	public class ChatEntry
	{
		public string FriendlyName { get; private set; }
		public byte[] Key { get; private set; }
		public string PreviewLine { get; private set; }

		public ChatEntry(string friendlyName, byte[] key, string previewLine)
		{
			FriendlyName = friendlyName;
			Key = key;
			PreviewLine = previewLine;
		}
	}

	public class Title
	{
		public string TitleString { get; set; }
		public int Index { get; set; }

		public Title(string title, int index)
		{
			TitleString = title;
			Index = index;
		}
	}
}


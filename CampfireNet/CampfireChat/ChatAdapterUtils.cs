
using System;
using Android.Views;
using Android.Widget;
using Android.Support.V7.Widget;
using System.Collections.Generic;

namespace CampfireChat
{
	class ChatAdapter : RecyclerView.Adapter
	{
		public List<MessageEntry> Entries;
		public event EventHandler<byte[]> ItemClick;

		private int selectedPos = -1;

		public ChatAdapter(List<MessageEntry> entries = null)
		{
			Entries = entries ?? new List<MessageEntry>();
		}

		public void AddEntry(MessageEntry entry, int position = -1)
		{
         if (position == -1) {
            position = Entries.Count;
         }
         Console.WriteLine($"              ##################### adding entry at {position} with text {entry.Message}");
			Entries.Insert(position, entry);
		}

		public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
		{
			View itemView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.MessageEntry, parent, false);

			ChatViewHolder vh = new ChatViewHolder(itemView, OnClick);
			return vh;
		}

		public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
		{
			ChatViewHolder vh = holder as ChatViewHolder;
			MessageEntry entry = Entries[position];

			if (selectedPos == position)
			{
				holder.ItemView.SetBackgroundColor(Android.Graphics.Color.LightGray);
			}
			else
			{
				holder.ItemView.SetBackgroundColor(Android.Graphics.Color.Transparent);
			}

			vh.Message.Text = entry.Message;
			vh.Name.Text = entry.Name;

			holder.ItemView.Selected = selectedPos == position;
		}

		private void OnClick(int position)
		{
			NotifyItemChanged(selectedPos);
			selectedPos = position;
			NotifyItemChanged(selectedPos);

			if (ItemClick != null)
			{
				byte[] id = { 0, 1, 2, 3 };
				ItemClick(this, id);
			}
		}

		public override int ItemCount
		{
			get { return Entries.Count; }
		}
	}

	public class ChatViewHolder : RecyclerView.ViewHolder
	{
		public TextView Message { get; private set; }
		public TextView Name { get; private set; }

		public ChatViewHolder(View itemView, Action<int> listener) : base(itemView)
		{
			Message = itemView.FindViewById<TextView>(Resource.Id.Message);
			Name = itemView.FindViewById<TextView>(Resource.Id.Name);

			itemView.Clickable = true;
			itemView.Click += (sender, e) => listener(AdapterPosition);
		}
	}

	public class MessageEntry
	{
		public string Name { get; private set; }
		public string Message { get; private set; }


		public MessageEntry(string name, string message)
		{
			Name = name;
			Message = message;
		}
	}
}
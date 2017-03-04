using System;
using Android.Views;
using Android.Widget;
using Android.Support.V7.Widget;

namespace CampfireChat
{
	class ChatlistAdapter : RecyclerView.Adapter
	{
		public ChatEntry[] Entries;
		public event EventHandler<Title> ItemClick;

		private int selectedPos = -1;

		public ChatlistAdapter(ChatEntry[] entries)
		{
			Entries = entries;
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

			if (selectedPos == position)
			{
				holder.ItemView.SetBackgroundColor(Android.Graphics.Color.LightGray);
			}
			else
			{
				holder.ItemView.SetBackgroundColor(Android.Graphics.Color.Transparent);
			}

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

			holder.ItemView.Selected = selectedPos == position;
		}

		private void OnClick(int position)
		{
			NotifyItemChanged(selectedPos);
			selectedPos = position;
			NotifyItemChanged(selectedPos);

			if (ItemClick != null)
			{
				string title = string.Join(", ", Entries[position].Names);
				ItemClick(this, new Title(title, position));
			}
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

		public ChatlistViewHolder(View itemView, Action<int> listener) : base(itemView)
		{
			Preview = itemView.FindViewById<TextView>(Resource.Id.Preview);
			Names = itemView.FindViewById<TextView>(Resource.Id.Names);

			itemView.Clickable = true;
			itemView.Click += (sender, e) => listener(base.AdapterPosition);
			//itemView.Touch += (object sender, View.TouchEventArgs e) => listener(base.AdapterPosition, e.Event.Action));
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
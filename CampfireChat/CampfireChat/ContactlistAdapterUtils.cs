
using Android.Views;
using Android.Widget;
using Android.Support.V7.Widget;
using System.Collections.Generic;
using System;

namespace CampfireChat
{
	class ContactlistAdapter : RecyclerView.Adapter
	{
		public ContactEntry[] Entries;
		public event EventHandler<byte[]> ItemClick;

		private HashSet<int> selectedPositions;

		public ContactlistAdapter(ContactEntry[] entries)
		{
			Entries = entries;
			selectedPositions = new HashSet<int>();
		}

		public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
		{
			View itemView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.ContactEntry, parent, false);

			ContactlistViewHolder vh = new ContactlistViewHolder(itemView, OnClick);
			return vh;
		}

		public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
		{
			ContactlistViewHolder vh = holder as ContactlistViewHolder;
			ContactEntry entry = Entries[position];
			vh.Name.Text = entry.Name;
			vh.Tag.Text = "(" + entry.Tag + ")";

			if (selectedPositions.Contains(position))
			{
				holder.ItemView.SetBackgroundColor(Android.Graphics.Color.LightGray);
			}
			else
			{
				holder.ItemView.SetBackgroundColor(Android.Graphics.Color.Transparent);
			}

			holder.ItemView.Selected = selectedPositions.Contains(position);
		}

		private void OnClick(int position)
		{
			if (selectedPositions.Contains(position))
			{
				selectedPositions.Remove(position);
			}
			else
			{
				selectedPositions.Add(position);
			}

			NotifyItemChanged(position);

			if (ItemClick != null)
			{
				byte[] id = { 0, 1, 2, 3 };
				ItemClick(this, id);
			}
		}

		public override int ItemCount
		{
			get { return Entries.Length; }
		}

		public void UpdateDataSet(ContactEntry[] newData)
		{
			Entries = newData;
			NotifyDataSetChanged();
		}
	}

	public class ContactlistViewHolder : RecyclerView.ViewHolder
	{
		public TextView Name { get; private set; }
		public TextView Tag { get; private set; }

		public ContactlistViewHolder(View itemView, Action<int> listener) : base(itemView)
		{
			Name = itemView.FindViewById<TextView>(Resource.Id.Name);
			Tag = itemView.FindViewById<TextView>(Resource.Id.Tag);

			itemView.Clickable = true;
			itemView.Click += (sender, e) => listener(AdapterPosition);
		}
	}

	public class ContactEntry
	{
		public string Name { get; private set; }
		public string Tag { get; private set; }

		public ContactEntry(string name, string tag = null)
		{
			Name = name;
			Tag = tag;
		}
	}
}
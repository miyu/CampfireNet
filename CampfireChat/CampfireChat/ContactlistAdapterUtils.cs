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
    class ContactlistAdapter : RecyclerView.Adapter
    {
        public ContactEntry[] Entries;

        public ContactlistAdapter(ContactEntry[] entries)
        {
            Entries = entries;
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            View itemView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.Contactlist, parent, false);

            ContactlistViewHolder vh = new ContactlistViewHolder(itemView);
            return vh;
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            ContactlistViewHolder vh = holder as ContactlistViewHolder;
            ContactEntry entry = Entries[position];
            vh.Name.Text = entry.Name;
            vh.Tag.Text = "(" + entry.Tag + ")";
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

        public ContactlistViewHolder(View itemView) : base(itemView)
        {
            Name = itemView.FindViewById<TextView>(Resource.Id.Name);
            Tag = itemView.FindViewById<TextView>(Resource.Id.Tag);
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
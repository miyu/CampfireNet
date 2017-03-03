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
        public string[] data;
        public class ChatlistViewHolder : RecyclerView.ViewHolder
        {
            public ImageView Image { get; private set; }
            public TextView Caption { get; private set; }

            public ChatlistViewHolder(View itemView) : base(itemView)
            {
                Image = itemView.FindViewById<ImageView>(Resource.Id.chat_image);
                Caption = itemView.FindViewById<TextView>(Resource.Id.text_preview);
            }
        }

        public ChatlistAdapter(string[] data)
        {
            this.data = data;
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            View itemView = LayoutInflater.From(parent.Context).
                        Inflate(Resource.Layout.ChatHistory, parent, false);

            ChatlistViewHolder vh = new ChatlistViewHolder(itemView);
            return vh;
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            ChatlistViewHolder vh = holder as ChatlistViewHolder;

            vh.Image.SetImageResource(position);

            vh.Caption.Text = data[position];
        }

        public override int ItemCount
        {
            get { return data.Length; }
        }
    }
}
using System;
using Android.Views;
using Android.Widget;
using Android.Support.V7.Widget;
using System.Collections.Generic;

namespace CampfireChat {
   class ChatlistAdapter : RecyclerView.Adapter {
      public List<ChatEntry> Entries { get; set; }
      public event EventHandler<byte[]> ItemClick;

      public ChatlistAdapter(List<ChatEntry> entries) {
         Entries = entries;
      }

      public void AddEntry(int position, ChatEntry entry) {
         Entries.Insert(position, entry);
         NotifyItemInserted(position);
      }

      public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType) {
         View itemView = LayoutInflater.From(parent.Context).Inflate(Resource.Layout.ChatEntry, parent, false);

         ChatlistViewHolder vh = new ChatlistViewHolder(itemView, OnClick);
         return vh;
      }

      public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position) {
         ChatlistViewHolder vh = holder as ChatlistViewHolder;
         ChatEntry entry = Entries[position];

         vh.FriendlyName.Text = entry.Context.FriendlyName;
         vh.Preview.Text = "Preview TBD";
      }

      private void OnClick(int position) {
         ItemClick?.Invoke(this, Entries[position].Id);
      }

      public override int ItemCount => Entries.Count;
   }

   public class ChatlistViewHolder : RecyclerView.ViewHolder {
      public TextView FriendlyName { get; private set; }
      public TextView Preview { get; private set; }

      public ChatlistViewHolder(View itemView, Action<int> listener) : base(itemView) {
         Preview = itemView.FindViewById<TextView>(Resource.Id.Preview);
         FriendlyName = itemView.FindViewById<TextView>(Resource.Id.FriendlyName);

         itemView.Clickable = true;
         itemView.Click += (sender, e) => listener(AdapterPosition);
         //itemView.Touch += (object sender, View.TouchEventArgs e) => listener(base.AdapterPosition, e.Event.Action));
      }
   }

   public class ChatEntry {
      public byte[] Id { get; private set; }
      public ChatRoomContext Context { get; private set; }

      public ChatEntry(byte[] id, ChatRoomContext context) {
         Id = id;
         Context = context;
      }
   }
}


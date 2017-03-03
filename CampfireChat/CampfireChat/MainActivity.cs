using Android.App;
using Android.Widget;
using Android.OS;
using Android.Views;
using Android.Content;
using Android.Support.V7.Widget;

namespace CampfireChat
{
    [Activity(Label = "CampfireChat", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : Activity
    {
        private RecyclerView chatlistRecyclerView;
        private RecyclerView.Adapter chatlistAdapter;
        private RecyclerView.LayoutManager chatlistLayoutManager;
        protected override void OnCreate(Bundle bundle)
        {
            string[] testData = { "Hello", "Nice", "Baby", "Rain", "Love", "Air", "Shoes", "Hair", "Perfume", "Obfuscation", "Clock", "Game", "Scroll", "Lion", "Chrome", "Tresure", "Charm"};
            base.OnCreate(bundle);
            SetContentView(Resource.Layout.Main);

            var toolbar = FindViewById<Android.Widget.Toolbar>(Resource.Id.toolbar);
            SetActionBar(toolbar);
            ActionBar.Title = "Chats";

            chatlistRecyclerView = (RecyclerView)FindViewById(Resource.Id.chatlist);
            chatlistRecyclerView.HasFixedSize = true;

            chatlistLayoutManager = new LinearLayoutManager(this);
            chatlistRecyclerView.SetLayoutManager(chatlistLayoutManager);

            chatlistAdapter = new ChatlistAdapter(testData);
            chatlistRecyclerView.SetAdapter(chatlistAdapter);
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.main_menu, menu);
            return base.OnCreateOptionsMenu(menu);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            Intent intent;
            switch(item.ItemId)
            {
                case Resource.Id.settings:
                    intent = new Intent(this, typeof(SettingsActivity));
                    StartActivity(intent);
                    break;
                case Resource.Id.menu_add:
                    intent = new Intent(this, typeof(NewChatActivity));
                    StartActivity(intent);
                    break;
            }
            return base.OnOptionsItemSelected(item);
        }
    }

}


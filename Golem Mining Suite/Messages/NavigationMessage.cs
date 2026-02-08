using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Golem_Mining_Suite.Messages
{
    public class NavigationMessage : ValueChangedMessage<string>
    {
        public NavigationMessage(string value) : base(value)
        {
        }
    }
}

using System.Windows.Input;

namespace ImmenseWall.ViewModels
{
    public class PasswordViewModel : ViewModelBase
    {
        public string PasswordHash { get; private set; }

        public string Password
        {
            get;
            set => SetProperty(ref field, value);
        }

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        public PasswordViewModel()
        {
            OkCommand = new RelayCommand(ExecuteOk);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        private void ExecuteOk()
        {
            PasswordHash = Hasher.HashString(Password);
        }

        private void ExecuteCancel()
        {
            // Cancel
        }
    }
}
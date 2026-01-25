using System.Windows.Input;

namespace ImmenseWall.ViewModels
{
    public class PasswordViewModel : ViewModelBase
    {
        private string _password;
        public string PasswordHash { get; private set; }

        public string Password
        {
            get => _password;
            set => SetField(ref _password, value);
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
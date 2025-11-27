namespace DarkSunRE.ViewModels
{
    public abstract partial class BaseDialogViewModel
    {
        public class DialogResultEventArgs : EventArgs
        {
            public bool? Result { get; }
            public object Data { get; }

            public DialogResultEventArgs(bool? result, object data)
            {
                Result = result;
                Data = data;
            }
        }

    }
}

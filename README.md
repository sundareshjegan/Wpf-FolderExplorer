# FolderExplorerControl

`FolderExplorerControl` is a custom WPF control designed to provide a folder selection functionality within a WPF application. Since WPF does not have native support for Folder Browser Dialogs, this control offers an integrated solution to select folders without relying on external libraries or references.

## Features

- **Folder Selection**: Allows users to browse and select folders.
- **No External Dependencies**: Does not require any external references or libraries.
- **Fully Integrated**: Seamlessly integrates with WPF applications.
- **Customizable**: Easily customizable to fit your application's needs.

## Usage/Examples

To use the `FolderExplorer` control in your WPF project, follow these steps:

1. **Add the Control to Your Project:**

   Copy the `FolderExplorerController` files into your project directory. Ensure that all necessary XAML and code files are included.
   
To use this control in your c# refer the following code snippet

   ```csharp
      private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
      {
         FolderExplorerControl folderExplorer = new FolderExplorerControl();
         string pathSelected = folderExplorer.SelectedPath;
         MessageBox.Show($"Selected Folder: {pathSelected}");
      }
   ```



## Screenshots
![Application Screenshot](https://github.com/user-attachments/assets/fe8d16f1-07ec-43c4-b388-70abd4c1b744)
![Shortcut Screenshot](https://github.com/user-attachments/assets/acc1d88d-a357-41d7-a363-27f65e6d7f35)


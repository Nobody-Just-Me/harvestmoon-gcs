using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using Windows.Foundation;

namespace HarvestmoonGCS.Helpers;

public static class AnimationHelper
{
    public static void FadeIn(FrameworkElement element, double duration = 0.3)
    {
        var storyboard = new Storyboard();
        var fadeAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromSeconds(duration),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(fadeAnimation, element);
        Storyboard.SetTargetProperty(fadeAnimation, "Opacity");
        
        storyboard.Children.Add(fadeAnimation);
        element.Opacity = 0;
        element.Visibility = Visibility.Visible;
        storyboard.Begin();
    }

    public static void FadeOut(FrameworkElement element, double duration = 0.3, Action? onComplete = null)
    {
        var storyboard = new Storyboard();
        var fadeAnimation = new DoubleAnimation
        {
            From = element.Opacity,
            To = 0,
            Duration = TimeSpan.FromSeconds(duration),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };

        Storyboard.SetTarget(fadeAnimation, element);
        Storyboard.SetTargetProperty(fadeAnimation, "Opacity");
        
        storyboard.Children.Add(fadeAnimation);
        
        if (onComplete != null)
        {
            storyboard.Completed += (s, e) => 
            {
                element.Visibility = Visibility.Collapsed;
                onComplete();
            };
        }
        else
        {
            storyboard.Completed += (s, e) => element.Visibility = Visibility.Collapsed;
        }
        
        storyboard.Begin();
    }

    public static void SlideIn(FrameworkElement element, double fromX = 50, double duration = 0.4)
    {
        var storyboard = new Storyboard();
        
        var slideAnimation = new DoubleAnimation
        {
            From = fromX,
            To = 0,
            Duration = TimeSpan.FromSeconds(duration),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
        };

        var fadeAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromSeconds(duration * 0.8),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(slideAnimation, element);
        Storyboard.SetTargetProperty(slideAnimation, "(UIElement.RenderTransform).(TranslateTransform.X)");
        
        Storyboard.SetTarget(fadeAnimation, element);
        Storyboard.SetTargetProperty(fadeAnimation, "Opacity");
        
        storyboard.Children.Add(slideAnimation);
        storyboard.Children.Add(fadeAnimation);
        
        element.Opacity = 0;
        element.Visibility = Visibility.Visible;
        storyboard.Begin();
    }

    public static void ScaleButton(FrameworkElement element, double scale = 1.05, double duration = 0.15)
    {
        var storyboard = new Storyboard();
        
        var scaleXAnimation = new DoubleAnimation
        {
            To = scale,
            Duration = TimeSpan.FromSeconds(duration),
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var scaleYAnimation = new DoubleAnimation
        {
            To = scale,
            Duration = TimeSpan.FromSeconds(duration),
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(scaleXAnimation, element);
        Storyboard.SetTargetProperty(scaleXAnimation, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)");
        
        Storyboard.SetTarget(scaleYAnimation, element);
        Storyboard.SetTargetProperty(scaleYAnimation, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)");
        
        storyboard.Children.Add(scaleXAnimation);
        storyboard.Children.Add(scaleYAnimation);
        storyboard.Begin();
    }

    public static void Pulse(FrameworkElement element, double minOpacity = 0.5, double duration = 1.0)
    {
        var storyboard = new Storyboard();
        storyboard.RepeatBehavior = RepeatBehavior.Forever;
        
        var pulseAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = minOpacity,
            Duration = TimeSpan.FromSeconds(duration),
            AutoReverse = true,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        Storyboard.SetTarget(pulseAnimation, element);
        Storyboard.SetTargetProperty(pulseAnimation, "Opacity");
        
        storyboard.Children.Add(pulseAnimation);
        storyboard.Begin();
    }

    /// <summary>
    /// Animates a smooth color transition for connection status changes
    /// </summary>
    public static void AnimateConnectionStatus(FrameworkElement element, bool isConnected, double duration = 0.5)
    {
        var storyboard = new Storyboard();
        
        // Fade animation
        var fadeAnimation = new DoubleAnimation
        {
            From = element.Opacity,
            To = isConnected ? 1.0 : 0.6,
            Duration = TimeSpan.FromSeconds(duration),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        Storyboard.SetTarget(fadeAnimation, element);
        Storyboard.SetTargetProperty(fadeAnimation, "Opacity");
        
        storyboard.Children.Add(fadeAnimation);
        storyboard.Begin();
    }

    /// <summary>
    /// Shake animation for error states or invalid input
    /// </summary>
    public static void Shake(FrameworkElement element, double intensity = 10, double duration = 0.5)
    {
        var storyboard = new Storyboard();
        
        var shakeAnimation = new DoubleAnimationUsingKeyFrames();
        shakeAnimation.Duration = TimeSpan.FromSeconds(duration);
        
        // Create shake keyframes
        shakeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0)), Value = 0 });
        shakeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.1)), Value = -intensity });
        shakeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.2)), Value = intensity });
        shakeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.3)), Value = -intensity * 0.7 });
        shakeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.4)), Value = intensity * 0.7 });
        shakeAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.5)), Value = 0 });

        Storyboard.SetTarget(shakeAnimation, element);
        Storyboard.SetTargetProperty(shakeAnimation, "(UIElement.RenderTransform).(TranslateTransform.X)");
        
        storyboard.Children.Add(shakeAnimation);
        storyboard.Begin();
    }

    /// <summary>
    /// Bounce animation for successful actions
    /// </summary>
    public static void Bounce(FrameworkElement element, double duration = 0.6)
    {
        var storyboard = new Storyboard();
        
        var bounceAnimation = new DoubleAnimationUsingKeyFrames();
        bounceAnimation.Duration = TimeSpan.FromSeconds(duration);
        
        // Create bounce keyframes
        bounceAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0)), Value = 1.0 });
        bounceAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.2)), Value = 1.2, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        bounceAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.4)), Value = 0.9, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });
        bounceAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.6)), Value = 1.0, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });

        Storyboard.SetTarget(bounceAnimation, element);
        Storyboard.SetTargetProperty(bounceAnimation, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)");
        
        storyboard.Children.Add(bounceAnimation);
        storyboard.Begin();
    }

    /// <summary>
    /// Smooth hover effect for buttons
    /// </summary>
    public static void ButtonHoverEnter(FrameworkElement element, double scale = 1.05, double duration = 0.2)
    {
        var storyboard = new Storyboard();
        
        var scaleXAnimation = new DoubleAnimation
        {
            To = scale,
            Duration = TimeSpan.FromSeconds(duration),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
        };

        var scaleYAnimation = new DoubleAnimation
        {
            To = scale,
            Duration = TimeSpan.FromSeconds(duration),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
        };

        var opacityAnimation = new DoubleAnimation
        {
            To = 1.0,
            Duration = TimeSpan.FromSeconds(duration),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(scaleXAnimation, element);
        Storyboard.SetTargetProperty(scaleXAnimation, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)");
        
        Storyboard.SetTarget(scaleYAnimation, element);
        Storyboard.SetTargetProperty(scaleYAnimation, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)");
        
        Storyboard.SetTarget(opacityAnimation, element);
        Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
        
        storyboard.Children.Add(scaleXAnimation);
        storyboard.Children.Add(scaleYAnimation);
        storyboard.Children.Add(opacityAnimation);
        storyboard.Begin();
    }

    /// <summary>
    /// Smooth hover exit effect for buttons
    /// </summary>
    public static void ButtonHoverExit(FrameworkElement element, double duration = 0.2)
    {
        var storyboard = new Storyboard();
        
        var scaleXAnimation = new DoubleAnimation
        {
            To = 1.0,
            Duration = TimeSpan.FromSeconds(duration),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var scaleYAnimation = new DoubleAnimation
        {
            To = 1.0,
            Duration = TimeSpan.FromSeconds(duration),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var opacityAnimation = new DoubleAnimation
        {
            To = 0.9,
            Duration = TimeSpan.FromSeconds(duration),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(scaleXAnimation, element);
        Storyboard.SetTargetProperty(scaleXAnimation, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)");
        
        Storyboard.SetTarget(scaleYAnimation, element);
        Storyboard.SetTargetProperty(scaleYAnimation, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)");
        
        Storyboard.SetTarget(opacityAnimation, element);
        Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
        
        storyboard.Children.Add(scaleXAnimation);
        storyboard.Children.Add(scaleYAnimation);
        storyboard.Children.Add(opacityAnimation);
        storyboard.Begin();
    }

    /// <summary>
    /// Rotate animation for loading indicators
    /// </summary>
    public static Storyboard StartRotation(FrameworkElement element, double duration = 2.0)
    {
        var storyboard = new Storyboard();
        storyboard.RepeatBehavior = RepeatBehavior.Forever;
        
        var rotateAnimation = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = TimeSpan.FromSeconds(duration)
            // LinearEase is default, no need to set EasingFunction
        };

        Storyboard.SetTarget(rotateAnimation, element);
        Storyboard.SetTargetProperty(rotateAnimation, "(UIElement.RenderTransform).(RotateTransform.Angle)");
        
        storyboard.Children.Add(rotateAnimation);
        storyboard.Begin();
        
        return storyboard; // Return so it can be stopped later
    }

    /// <summary>
    /// Highlight animation for important notifications
    /// </summary>
    public static void Highlight(FrameworkElement element, double duration = 0.8)
    {
        var storyboard = new Storyboard();
        
        var scaleAnimation = new DoubleAnimationUsingKeyFrames();
        scaleAnimation.Duration = TimeSpan.FromSeconds(duration);
        
        scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0)), Value = 1.0 });
        scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.3)), Value = 1.15, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.8)), Value = 1.0, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });

        Storyboard.SetTarget(scaleAnimation, element);
        Storyboard.SetTargetProperty(scaleAnimation, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)");
        
        var scaleYAnimation = new DoubleAnimationUsingKeyFrames();
        scaleYAnimation.Duration = TimeSpan.FromSeconds(duration);
        
        scaleYAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0)), Value = 1.0 });
        scaleYAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.3)), Value = 1.15, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        scaleYAnimation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.8)), Value = 1.0, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });

        Storyboard.SetTarget(scaleYAnimation, element);
        Storyboard.SetTargetProperty(scaleYAnimation, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)");
        
        storyboard.Children.Add(scaleAnimation);
        storyboard.Children.Add(scaleYAnimation);
        storyboard.Begin();
    }

    /// <summary>
    /// Smooth transition for tab switching
    /// </summary>
    public static void TabSwitch(FrameworkElement outElement, FrameworkElement inElement, double duration = 0.3)
    {
        // Fade out old tab
        FadeOut(outElement, duration * 0.5);
        
        // Fade in and slide in new tab
        var storyboard = new Storyboard();
        
        var slideAnimation = new DoubleAnimation
        {
            From = 30,
            To = 0,
            Duration = TimeSpan.FromSeconds(duration),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var fadeAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromSeconds(duration),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(slideAnimation, inElement);
        Storyboard.SetTargetProperty(slideAnimation, "(UIElement.RenderTransform).(TranslateTransform.X)");
        
        Storyboard.SetTarget(fadeAnimation, inElement);
        Storyboard.SetTargetProperty(fadeAnimation, "Opacity");
        
        storyboard.Children.Add(slideAnimation);
        storyboard.Children.Add(fadeAnimation);
        
        inElement.Opacity = 0;
        inElement.Visibility = Visibility.Visible;
        storyboard.Begin();
    }
}